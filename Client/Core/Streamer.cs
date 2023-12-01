#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Client.ChannelInterface;
using Client.Interface;
using Newtonsoft.Json.Linq;

#endregion

namespace Client.Core;

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
        reader.ReadDouble(); // the rank is ignored for single objects
        var dataSize = reader.ReadInt32();
        var data = reader.ReadBytes(dataSize);

        var mode = SerializationMode.Json;
        if (useProtocolBuffers)
            mode = SerializationMode.ProtocolBuffers;

        return SerializationHelper.ObjectFromBytes<TItem>(data, mode, useCompression);
    }

    public static async Task<byte[]> ReadDataAsync(Stream stream, int dataSize)
    {
        var result = new byte[dataSize];

        var offset = 0;

        while (offset < dataSize)
        {
            var read = await stream.ReadAsync(result, offset, dataSize - offset);
            offset += read;
        }

        return result;
    }


    public static async Task<TItem> FromStreamAsync<TItem>(Stream stream)
    {
        // itemCount always one here

        var buffer = await ReadDataAsync(stream, sizeof(int));
        var itemCount = BitConverter.ToInt32(buffer, 0);

        if (itemCount != 1)
        {
            var msg = $"Waiting for one object, found {itemCount}";
            throw new StreamingException(msg);
        }

        // use protocol buffers or json
        buffer = await ReadDataAsync(stream, sizeof(bool));
        var useProtocolBuffers = BitConverter.ToBoolean(buffer, 0);

        // use compression
        buffer = await ReadDataAsync(stream, sizeof(bool));
        var useCompression = BitConverter.ToBoolean(buffer, 0);

        // read and ignore the rank (not used for single objects)
        await ReadDataAsync(stream, sizeof(double));

        // size of raw data
        buffer = await ReadDataAsync(stream, sizeof(int));
        var dataSize = BitConverter.ToInt32(buffer, 0);

        // raw data
        var data = await ReadDataAsync(stream, dataSize);

        var mode = SerializationMode.Json;
        if (useProtocolBuffers)
            mode = SerializationMode.ProtocolBuffers;

        return SerializationHelper.ObjectFromBytes<TItem>(data, mode, useCompression);
    }

    public static void ToStream<TItem>(Stream stream, TItem item, CollectionSchema collectionSchema = null)
    {
        var bufferedStream = new BufferedStream(stream);


        var useProtocolBuffers =
            collectionSchema == null; // use protocol buffers only for requests not for business objects
        var useCompression = collectionSchema != null && collectionSchema.StorageLayout == Layout.Compressed;

        var mode = SerializationMode.ProtocolBuffers;
        if (!useProtocolBuffers)
            mode = SerializationMode.Json;

        var writer = new BinaryWriter(bufferedStream);

        const int itemCount = 1;
        writer.Write(itemCount);
        var data = SerializationHelper.ObjectToBytes(item, mode, useCompression);
        writer.Write(useProtocolBuffers);
        writer.Write(useCompression);
        writer.Write(0D);
        writer.Write(data.Length);
        writer.Write(data);
        writer.Flush();
    }


    /// <summary>
    ///     Stream a collection of generic objects. Do not use for <see cref="PackedObject" />
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
    ///     Special version for <see cref="PackedObject" />. As they already contain the serialized object
    ///     no need to serialize again
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="items"></param>
    /// <param name="selectedIndexes">if at least one is specified do not send the whole object but only the specified values</param>
    /// <param name="aliases"></param>
    public static void ToStreamMany(Stream stream, ICollection<PackedObject> items, int[] selectedIndexes,
                                    string[] aliases)
    {
        var bufferedStream = new BufferedStream(stream);
        var writer = new BinaryWriter(bufferedStream);


        var itemCount = items.Count;
        writer.Write(itemCount);

        foreach (var item in items)
        {
            var data = selectedIndexes.Length > 0 ? item.GetData(selectedIndexes, aliases) : item.ObjectData;

            writer.Write(false);
            writer.Write(item.Layout == Layout.Compressed);
            writer.Write(item.Rank);
            writer.Write(data.Length);
            writer.Write(data);
        }

        writer.Flush();
    }

    public static void ToStreamMany(Stream stream, ICollection<JObject> items)
    {
        var bufferedStream = new BufferedStream(stream);
        var writer = new BinaryWriter(bufferedStream);


        var itemCount = items.Count;
        writer.Write(itemCount);

        foreach (var item in items)
        {
            var data = SerializationHelper.ObjectToBytes(item, SerializationMode.Json, false);

            writer.Write(false);
            writer.Write(false);
            writer.Write((double)0); // no rank
            writer.Write(data.Length);
            writer.Write(data);
        }

        writer.Flush();
    }


    public static IEnumerable<RankedItem> EnumerableFromStream(Stream stream)
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

            using var memStream = new MemoryStream(data);

            var mode = SerializationMode.Json;
            if (useProtocolBuffers)
                mode = SerializationMode.ProtocolBuffers;

            var deserializationFailure = false;
            JObject result = null;
            try
            {
                result = SerializationHelper.ObjectFromStream<JObject>(memStream, mode, useCompression);
                if (!result.HasValues)
                    deserializationFailure = true;
            }
            catch (Exception)
            {
                deserializationFailure = true;
            }

            if (deserializationFailure)
            {
                memStream.Seek(0, SeekOrigin.Begin);
                var exception = SerializationHelper.ObjectFromStream<ExceptionResponse>(memStream, mode,
                    useCompression);

                if (exception != null) throw new CacheException("Exception from server:" + exception.Message);

                var message = "Received an unknown item type while expecting JObject";
                throw new StreamingException(message);
            }


            yield return new(rank, result);
        }
    }

    public static void FromStream<TItemType>(Stream stream, DataHandler<TItemType> dataHandler,
                                             ExceptionHandler exceptionHandler)
    {
        if (dataHandler == null)
            throw new ArgumentNullException(nameof(dataHandler));
        if (exceptionHandler == null)
            throw new ArgumentNullException(nameof(exceptionHandler));

        
        var reader = new BinaryReader(stream);


        var items = reader.ReadInt32();
        for (var i = 0; i < items; i++)
        {
            var useProtocolBuffers = reader.ReadBoolean();
            var useCompression = reader.ReadBoolean();
            reader.ReadDouble(); // the rank is not used in this case
            var dataSize = reader.ReadInt32();

            var data = reader.ReadBytes(dataSize);

            using var memStream = new MemoryStream(data);

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
                    dataHandler((TItemType)result, i + 1, items);
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
                        exceptionHandler((ExceptionResponse)result);
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

    public static void SendAck(Stream stream)
    {
        var bufferedStream = new BufferedStream(stream);
        var writer = new BinaryWriter(bufferedStream);
        writer.Write(Ack);
        writer.Flush();
    }

    public static bool ReadAck(Stream stream)
    {
        var reader = new BinaryReader(stream);
        var ack = reader.ReadInt64();
        Dbg.CheckThat(ack == Ack);

        return ack == Ack;
    }
}