#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using Client.ChannelInterface;
using Client.Interface;
using Client.Messages;

#endregion

namespace Client.Core
{
    /// <summary>
    ///     Store/restore objects to/from streams in a network friendly format
    ///     Collections of identical objects are streamed like [items count] + [item1 size] + [item1 data] +  [item2 size] +
    ///     [item2 data] .......
    /// </summary>
    public static class Streamer
    {
        private const long Ack = 0xABCDEF00;

        public static TItem FromStream<TItem>(Stream stream)
        {
            var reader = new BinaryReader(stream);

            var itemCount = reader.ReadInt32();
            if (itemCount != 1)
            {
                var msg = $"Waiting for one object, found {itemCount}";
                throw new StreamingException(msg);
            }

            var useProtocolBuffers = reader.ReadBoolean();
            var useCompression = reader.ReadBoolean();
            reader.ReadDouble();// the rank is ignored for single objects
            var dataSize = reader.ReadInt32();
            var data = reader.ReadBytes(dataSize);

            var mode = SerializationMode.Json;
            if (useProtocolBuffers)
                mode = SerializationMode.ProtocolBuffers;

            return SerializationHelper.ObjectFromBytes<TItem>(data, mode, useCompression);
        }

        public static void ToStream<TItem>(Stream stream, TItem item, TypeDescription typeDescription = null)
        {
            var bufferedStream = new BufferedStream(stream);


            var useProtocolBuffers =
                typeDescription == null; // use protocol buffers only for requests not for business objects
            var useCompression = typeDescription != null && typeDescription.UseCompression;

            var mode = SerializationMode.ProtocolBuffers;
            if (!useProtocolBuffers)
                mode = SerializationMode.Json;

            var writer = new BinaryWriter(bufferedStream);

            const int itemCount = 1;
            writer.Write(itemCount);
            var data = SerializationHelper.ObjectToBytes(item, mode, typeDescription);
            writer.Write(useProtocolBuffers);
            writer.Write(useCompression);
            writer.Write(0D);
            writer.Write(data.Length);
            writer.Write(data);
            writer.Flush();
        }


        /// <summary>
        ///     Stream a collection of generic objects. Do not use for <see cref="CachedObject" />
        /// </summary>
        /// <typeparam name="TItemType"></typeparam>
        /// <param name="stream"></param>
        /// <param name="items"></param>
        public static void ToStreamGeneric<TItemType>(Stream stream, ICollection<TItemType> items)
            where TItemType : class
        {
            var bufferedStream = new BufferedStream(stream);
            var writer = new BinaryWriter(bufferedStream);
            using (var memStream = new MemoryStream())
            {
                var itemCount = items.Count;
                writer.Write(itemCount);


                foreach (var item in items)
                {
                    SerializationHelper.ObjectToStream(item, memStream, SerializationMode.ProtocolBuffers, false);
                    var data = memStream.GetBuffer();
                    writer.Write(true); //serialized using protocol buffers
                    writer.Write(false);
                    //no compression for generic objects as we use protocol buffers which are very compact
                    writer.Write(0D); // the rank is not used in this case
                    writer.Write(data.Length);
                    writer.Write(data);

                    memStream.Seek(0, SeekOrigin.Begin);
                }
            }

            writer.Flush();
        }


        /// <summary>
        ///     Special version for <see cref="CachedObject" />. As they already contain the serialized object
        ///     no need to serialize again
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="items"></param>
        public static void ToStreamMany(Stream stream, ICollection<CachedObject> items)
        {
            var bufferedStream = new BufferedStream(stream);
            var writer = new BinaryWriter(bufferedStream);


            var itemCount = items.Count;
            writer.Write(itemCount);

            foreach (var item in items)
            {
                var data = item.ObjectData;
                writer.Write(false);
                writer.Write(item.UseCompression);
                writer.Write(item.Rank);
                writer.Write(data.Length);
                writer.Write(data);
            }

            writer.Flush();
        }


        public static IEnumerable<RankedItem> EnumerableFromStream<TItemType>(Stream stream)
        {
            var reader = new BinaryReader(stream);

            var items = reader.ReadInt32();
            for (var i = 0; i < items; i++)
            {
                var useProtocolBuffers = reader.ReadBoolean();
                var useCompression = reader.ReadBoolean();
                var rank = reader.ReadDouble();
                var dataSize = reader.ReadInt32();
                var data = reader.ReadBytes(dataSize);

                using (var memStream = new MemoryStream(data))
                {
                    var mode = SerializationMode.Json;
                    if (useProtocolBuffers)
                        mode = SerializationMode.ProtocolBuffers;

                    var deserializationFailure = false;
                    object result = null;
                    try
                    {
                        result = SerializationHelper.ObjectFromStream<TItemType>(memStream, mode, useCompression);
                    }
                    catch (Exception)
                    {
                        deserializationFailure = true;
                    }

                    if (deserializationFailure)
                    {
                        memStream.Seek(0, SeekOrigin.Begin);
                        result = SerializationHelper.ObjectFromStream<ExceptionResponse>(memStream, mode,
                            useCompression);

                        if (result != null)
                        {
                            var exc = (ExceptionResponse) result;
                            throw new CacheException("Exception from server:" + exc.Message);
                        }

                        var message = $"Received an unknown item type while expecting {typeof(TItemType)}";
                        throw new StreamingException(message);
                    }

                    

                    yield return new RankedItem{Item = result, Rank = rank};
                }
            }
        }

        public static void FromStream<TItemType>(Stream stream, DataHandler<TItemType> dataHandler,
            ExceptionHandler exceptionHandler)
        {
            if (dataHandler == null)
                throw new ArgumentNullException(nameof(dataHandler));
            if (exceptionHandler == null)
                throw new ArgumentNullException(nameof(exceptionHandler));

            //BufferedStream bufferedStream = new BufferedStream(stream);
            var reader = new BinaryReader(stream);


            var items = reader.ReadInt32();
            for (var i = 0; i < items; i++)
            {
                var useProtocolBuffers = reader.ReadBoolean();
                var useCompression = reader.ReadBoolean();
                reader.ReadDouble(); // the rank is not used in this case
                var dataSize = reader.ReadInt32();

                var data = reader.ReadBytes(dataSize);

                using (var memStream = new MemoryStream(data))
                {
                    try
                    {
                        var mode = SerializationMode.Json;
                        if (useProtocolBuffers)
                            mode = SerializationMode.ProtocolBuffers;

                        var deserializationFailure = false;
                        object result;
                        try
                        {
                            result = SerializationHelper.ObjectFromStream<TItemType>(memStream, mode, useCompression);
                            dataHandler((TItemType) result, i + 1, items);
                        }
                        catch (Exception)
                        {
                            deserializationFailure = true;
                        }

                        if (deserializationFailure)
                        {
                            memStream.Seek(0, SeekOrigin.Begin);
                            result = SerializationHelper.ObjectFromStream<ExceptionResponse>(memStream, mode,
                                useCompression);

                            if (result != null)
                            {
                                exceptionHandler((ExceptionResponse) result);
                            }
                            else
                            {
                                var message = $"Received an unknown item type while expecting {typeof(TItemType)}";
                                throw new StreamingException(message);
                            }
                        }
                    }
                    catch (IOException ex)
                    {
                        var exResponse = new ExceptionResponse(ex);
                        exceptionHandler(exResponse);
                    }
                    catch (SerializationException ex)
                    {
                        var exResponse = new ExceptionResponse(ex);
                        exceptionHandler(exResponse);
                    }
                }
            }
        }

        public static void SendAck(Stream stream)
        {
            var bufferedStream = new BufferedStream(stream);
            var writer = new BinaryWriter(bufferedStream);
            writer.Write(Ack);
            writer.Flush();
        }

        public static bool ReadAck(Stream stream)
        {
            //BufferedStream bufferedStream = new BufferedStream(stream);
            var reader = new BinaryReader(stream);
            var ack = reader.ReadInt64();
            Dbg.CheckThat(ack == Ack);

            return ack == Ack;
        }

        public static void ToStream<THeader>(MemoryStream stream, THeader header, ICollection<CachedObject> items)
            where THeader : class
        {
            var bufferedStream = new BufferedStream(stream);
            var writer = new BinaryWriter(bufferedStream);

            using (var memStream = new MemoryStream())
            {
                const int oneItemCount = 1;
                writer.Write(oneItemCount);
                SerializationHelper.ObjectToStream(header, memStream, SerializationMode.ProtocolBuffers, false);
                var data = memStream.GetBuffer();
                writer.Write(true); //protocol buffers used for header serialization
                writer.Write(false); //the header is not compressed
                writer.Write(data.Length);
                writer.Write(data);
            }

            writer.Flush();

            var itemCount = items.Count;
            writer.Write(itemCount);

            foreach (var item in items)
            {
                var data = item.ObjectData;
                writer.Write(false); // no protobuf for objects
                writer.Write(item.UseCompression);
                writer.Write(item.Rank);
                writer.Write(data.Length);
                writer.Write(data);
            }

            writer.Flush();
        }
    }
}