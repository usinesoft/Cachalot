using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Client.Core;
using Client.Tools;

namespace Server.FullTextSearch
{
    public class FullTextIndex
    {
        private class ScoreByPointer
        {
            private readonly Dictionary<LinePointer, double> _scoreByPointer = new Dictionary<LinePointer, double>();

            public double this[LinePointer pointer]
            {
                get
                {
                    if (_scoreByPointer.TryGetValue(pointer, out var score)) return score;

                    return 0;
                }

                set => _scoreByPointer[pointer] = value;
            }
        }


        public ITrace Trace { get; set; } = new NullTrace();


        public void Clear()
        {
            Entries = 0;
            IgnoredTokens = 0;
            MaxCapacity = 10_000_000;

            PositionsByToken.Clear();
            PositionsByDocument.Clear();
        }


        /// <summary>
        ///     Score bonus im more than one tokens on the same line
        /// </summary>
        private static readonly int SameLineMultiplier = 1000;

        /// <summary>
        ///     Score bonus im more than one tokens in the same document
        /// </summary>
        private static readonly int SameDocumentMultiplier = 100;

        /// <summary>
        ///     Maximum entries in the index
        /// </summary>
        private int MaxCapacity { get; set; } = 10_000_000;

        /// <summary>
        ///     Maximum of tokens that can be deleted wile trying to limit the entries (the most frequent ones will be deleted)
        /// </summary>
        private int MaxTokensToIgnore { get; } = 100;

        public int Entries { get; private set; }

        public int IgnoredTokens { get; private set; }


        /// <summary>
        ///     This is the main index used for search operations
        /// </summary>
        public Dictionary<string, HashSet<LinePointer>> PositionsByToken { get; } =
            new Dictionary<string, HashSet<LinePointer>>();

        /// <summary>
        ///     This is a secondary index used for update and delete
        /// </summary>
        private Dictionary<KeyValue, List<LinePointer>> PositionsByDocument { get; } =
            new Dictionary<KeyValue, List<LinePointer>>();

        public FullTextIndex(int maxCapacity = 0)
        {
            if (maxCapacity != 0) MaxCapacity = maxCapacity;
        }


        /// <summary>
        ///     Add a tokenized line to the full-text index
        /// </summary>
        /// <param name="line"></param>
        /// <param name="lineIndex"></param>
        /// <param name="primaryKey"></param>
        private void IndexLine(TokenizedLine line, int lineIndex, KeyValue primaryKey)
        {
            var pointer = new LinePointer(lineIndex, primaryKey);

            foreach (var token in line.Tokens)
            {
                var tooFrequentToken = false;

                if (!PositionsByToken.TryGetValue(token, out var positions))
                {
                    positions = new HashSet<LinePointer>();
                    PositionsByToken[token] = positions;
                }
                else
                {
                    if (positions.Count == 0) tooFrequentToken = true;
                }

                if (!tooFrequentToken)
                    if (positions.Add(pointer))
                    {
                        Entries = Entries + 1;

                        AddToSecondaryIndex(pointer);
                    }
            }


            // Remove the most frequent (less discriminant) tokens in the index if the index is too big
            // Limit the entries in the index: try to limit to MaxCapacity but without removing more than MaxTokensToIgnore tokens
            if (IgnoredTokens < MaxTokensToIgnore && Entries > MaxCapacity)
            {
                string mostFrequentToken = null;

                var maxFrequency = 0;

                foreach (var p in PositionsByToken)
                    if (p.Value.Count > maxFrequency)
                    {
                        mostFrequentToken = p.Key;
                        maxFrequency = p.Value.Count;
                    }

                Debug.Assert(mostFrequentToken != null);


                // leave an empty list. This will indicate that this token is not indexed any more
                var pointers = PositionsByToken[mostFrequentToken];
                pointers.Clear();

                Entries = Entries - maxFrequency;

                IgnoredTokens++;
            }
        }


        /// <summary>
        ///     The secondary index is used when a document is deleted or updated
        /// </summary>
        /// <param name="pointer"></param>
        private void AddToSecondaryIndex(LinePointer pointer)
        {
            if (!PositionsByDocument.TryGetValue(pointer.PrimaryKey, out var list))
            {
                list = new List<LinePointer>();
                PositionsByDocument.Add(pointer.PrimaryKey, list);
            }

            list.Add(pointer);
        }


        public void DeleteDocument(KeyValue primaryKey)
        {
            if (PositionsByDocument.TryGetValue(primaryKey, out var pointers))
            {
                foreach (var pointer in pointers)
                {
                    pointer.Deleted = true;
                    Entries--;
                }
            }            
        }


        /// <summary>
        ///     Index a document. A document is an ordered sequence of lines
        /// </summary>
        /// <param name="documentContent"></param>
        /// <param name="primaryKey"></param>
        public void IndexDocument(string[] documentContent, KeyValue primaryKey)
        {
            // update = delete + insert
            if (PositionsByDocument.ContainsKey(primaryKey)) DeleteDocument(primaryKey);

            var tokenizer = new Tokenizer();

            var lines = documentContent;
            var tokenizedLines = tokenizer.Tokenize(lines);

            var lineIndex = 0;
            foreach (var line in tokenizedLines)
            {
                IndexLine(line, lineIndex, primaryKey);

                lineIndex++;
            }
        }


        private IList<string> OrderByFrequency(string query)
        {
            var tokenizer = new Tokenizer();
            var tokens = tokenizer.TokenizeOneLine(query).Where(t => t.TokenType != CharClass.Symbol);

            var frequencyByToken = new Dictionary<string, int>();

            foreach (var token in tokens)
            {
                var frequency = 0;
                if (PositionsByToken.TryGetValue(token.Text, out var list)) frequency = list.Count;

                if (frequency != 0) frequencyByToken[token.Text] = frequency;
            }

            if (frequencyByToken.Count == 0) return new List<string>();


            // most significant (less frequent) tokens first
            return frequencyByToken.OrderBy(p => p.Value).Select(p => p.Key).Distinct().ToList();
        }

        /// <summary>
        ///     Find lines that match a full-text query
        /// </summary>
        /// <param name="query"></param>
        /// <param name="maxResults"></param>
        /// <returns></returns>
        public Dictionary<LinePointer, double> Find(string query, int maxResults)
        {
            Dictionary<LinePointer, double> sameLineResult = null;

            Dictionary<LinePointer, double> sameDocResult = null;

            // run different search strategies in parallel
            Parallel.Invoke(
                () => { sameLineResult = SameLineFind(query); },
                () => { sameDocResult = SameDocumentFind(query); }
            );

            


            var mergedResult = new Dictionary<LinePointer, double>(sameLineResult);

            foreach (var pair in sameDocResult)
                if (mergedResult.TryGetValue(pair.Key, out var score))
                {
                    if (pair.Value > score) // a better score with the second strategy
                        mergedResult[pair.Key] = pair.Value;
                }
                else
                {
                    mergedResult[pair.Key] = pair.Value;
                }

            return mergedResult;
        }


        /// <summary>
        ///     Find the documents with the highest score (a document score is the some of its line's scores)
        /// </summary>
        /// <param name="query"></param>
        /// <param name="maxResults"></param>
        /// <returns></returns>
        public IList<SearchResult> SearchBestDocuments(string query, int maxResults)
        {
            var documents =  Find(query, maxResults)
                .GroupBy(p => p.Key.PrimaryKey)
                .Select(g => new SearchResult
                {
                    PrimaryKey = g.Key,
                    Score = g.Sum(p => p.Value),
                    LinePointers = g.OrderBy(p => p.Key.Line).Select(p => p.Key).ToList()
                })
                .OrderByDescending(d => d.Score);

            if (maxResults > 0)
            {
                return documents.Take(maxResults).ToList();
            }
                
            return documents.ToList();
        }


        /// <summary>
        ///     Search strategy that favors the lines containing more than one token from the query
        /// </summary>        
        /// <param name="query"></param>
        /// <returns>score for each found pointer</returns>
        private Dictionary<LinePointer, double> SameLineFind(string query)
        {
            var orderedTokens = OrderByFrequency(query);

            if (orderedTokens.Count == 0)
            {
                // none of the tokens in the query was found
                return new Dictionary<LinePointer, double>();
            }


            var result = new Dictionary<LinePointer, double>();
            
            var foundTokens = 1;

            Trace?.Trace("Same line strategy");

            var scores = new ScoreByPointer();

            var tokensByLine = new Dictionary<LinePointer, List<string>>();

            foreach (var tk in orderedTokens)
            {
                var positions = PositionsByToken[tk];

                foreach (var position in positions.Where(p=>p.Deleted == false))
                {
                    if (!tokensByLine.TryGetValue(position, out var tokens))
                    {
                        tokens = new List<string>();
                        tokensByLine[position] = tokens;
                    }
                    
                    tokens.Add(tk);
                }

                double score = 0;
                if (positions.Count != 0)
                {
                    score = Math.Log10((double) Entries / positions.Count);                    
                }

                foreach (var pointer in positions) 
                    scores[pointer] += score;
                
            }

            foreach (var pair in tokensByLine.Where(p=>p.Value.Count > 1))
            {
                
                result[pair.Key] = scores[pair.Key] *= SameLineMultiplier * foundTokens;

            }

            var maxScore = result.Max(p => p.Value);

            return result.Where(p=>p.Value > maxScore / 100).ToDictionary(p=>p.Key, p=>p.Value);
        }


        /// <summary>
        ///     Search strategy that favors multiple tokens in the same document (not on the same line)
        ///    Also works for single tokens per document
        /// </summary>       
        /// <param name="query"></param>
        /// <returns></returns>
        private Dictionary<LinePointer, double> SameDocumentFind(string query)
        {
            var orderedTokens = OrderByFrequency(query);

            var result = new HashSet<LinePointer>();

            Trace?.Trace("Same document strategy");

            
            var differentTokensByDocument = new Dictionary<KeyValue, HashSet<string>>();

            var scores = new ScoreByPointer();

            foreach (var tk in orderedTokens)
            {
                var positions = PositionsByToken[tk];

                var score = Math.Log10((double) Entries / positions.Count);

                var plist = positions.Where(p => !p.Deleted).ToList();


                foreach (var pointer in plist)
                {
                    if (!differentTokensByDocument.TryGetValue(pointer.PrimaryKey, out var tset))
                    {
                        tset = new HashSet<string>();
                        differentTokensByDocument[pointer.PrimaryKey] = tset;
                    }

                    tset.Add(tk);

                    scores[pointer] += score;

                    result.Add(pointer);
                }
                
            }

            // better score if different tokens found in the same document
            
            foreach (var linePointer in result)
            {
                var tokens = differentTokensByDocument[linePointer.PrimaryKey].Count;
                if (tokens > 1)
                {
                    scores[linePointer] *= tokens * SameDocumentMultiplier;
                }
            }

            var maxScore = result.Max( p => scores[p]);
            
            return result.Where(p=>scores[p] > maxScore / 100).ToDictionary(p => p, p => scores[p]);
        }
    }

    public interface ITrace
    {
        void Trace(string line);
    }

    internal class NullTrace : ITrace
    {
        public void Trace(string line)
        {
        }
    }
}