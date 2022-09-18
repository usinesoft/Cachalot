using Client;
using Client.Core;
using Client.Tools;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Server.FullTextSearch
{
    public class FullTextIndex
    {
        class LineResult
        {
            public double Score { get; set; }

            public IList<string> TokensFound { get; set; }

        }

        /// <summary>
        ///     Score bonus im more than one tokens on the same line
        /// </summary>
        private static readonly int SameLineMultiplier = 100;
        private static readonly int TokenCoverageMultiplier = 1;

        private readonly FullTextConfig _ftConfig;

        public FullTextIndex(FullTextConfig ftConfig)
        {
            _ftConfig = ftConfig ?? new FullTextConfig();

            var tokensToIgnore = _ftConfig.TokensToIgnore ?? new List<string>();
            foreach (var token in tokensToIgnore) IgnoreToken(token);
        }

        /// <summary>
        ///     A function that allows to retrieve the text of the original line pointed by a line pointer
        ///     If provided, it is used for finer score computing (order of tokens inside the line is taken into account)
        /// </summary>
        public Func<LinePointer, TokenizedLine> LineProvider { get; set; }


        private ITrace Trace { get; } = new NullTrace();


        private int Entries { get; set; }

        private int IgnoredTokens { get; set; }


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


        public void Clear()
        {
            Entries = 0;
            IgnoredTokens = 0;

            PositionsByToken.Clear();
            PositionsByDocument.Clear();
        }


        /// <summary>
        ///     Compute a score bonus (a multiplier to be applied on the previously computed score) if the order of tokens is
        ///     preserved between
        ///     the query and the found line. Exact sequences give a bigger bonus
        /// </summary>
        /// <param name="query"></param>
        /// <param name="line"></param>
        /// <returns></returns>
        public static double ComputeBonusIfOrderIsPreserved(TokenizedLine query, TokenizedLine line)
        {
            var first = query.Tokens;

            var second = line.Tokens;


            // index in query --> index in line or -1 if correspondent token not found
            var indexes = new List<KeyValuePair<int, int>>();

            var index1 = 0;

            foreach (var token in first)
            {
                var index2 = -1;

                for (var i = 0; i < second.Count; i++)
                    if (second[i] == token)
                    {
                        index2 = i;
                        break;
                    }

                indexes.Add(new KeyValuePair<int, int>(index1, index2));

                index1++;
            }

            // make the indexes in the second sequence 0 based
            var min = indexes.Min(p => p.Value >= 0 ? p.Value : 0);
            indexes = indexes.Where(p => p.Value >= 0).Select(p => new KeyValuePair<int, int>(p.Key, p.Value - min))
                .ToList();

            double scoreMultiplier = 1;
            for (var i = 1; i < indexes.Count; i++)
            {
                var prev1 = indexes[i - 1].Key;
                var curr1 = indexes[i].Key;

                var distance1 = curr1 - prev1;

                var prev2 = indexes[i - 1].Value;
                var curr2 = indexes[i].Value;

                var distance2 = curr2 - prev2;


                if (distance1 == distance2)
                    scoreMultiplier *= 100;
                else if (distance2 - distance1 == 1)
                    scoreMultiplier *= 50;
                else if (distance2 - distance1 == 2)
                    scoreMultiplier *= 30;
                else if (distance2 > 0) // still apply a bonus because order is preserved 
                    scoreMultiplier *= 2;
            }


            return scoreMultiplier;
        }


        private bool NeedsCleanup()
        {
            var ignoredTokensLimitWasNotReached =
                _ftConfig.MaxTokensToIgnore == 0 || IgnoredTokens < _ftConfig.MaxTokensToIgnore;

            var tooManyEntries = _ftConfig.MaxIndexedTokens != 0 && Entries > _ftConfig.MaxIndexedTokens;

            return tooManyEntries && ignoredTokensLimitWasNotReached;
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
            if (NeedsCleanup())
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

                IgnoreToken(mostFrequentToken);


                Entries = Entries - maxFrequency;

                IgnoredTokens++;
            }
        }

        private void IgnoreToken(string toIgnore)
        {
            // adding an empty collection means this token must not be indexed (either explicitly specified in the config or removed if the maximum index size is reached)
            PositionsByToken[toIgnore] = new HashSet<LinePointer>();
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
                foreach (var pointer in pointers)
                    if (!pointer.Deleted)
                    {
                        pointer.Deleted = true;
                        Entries--;
                    }
        }


        /// <summary>
        ///     Index a document. A document is an ordered sequence of lines
        /// </summary>
        public void IndexDocument([NotNull] PackedObject item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));


            var primaryKey = item.PrimaryKey;

            // update = delete + insert
            if (PositionsByDocument.ContainsKey(primaryKey)) DeleteDocument(primaryKey);


            IList<TokenizedLine> lines;

            if (item.TokenizedFullText != null)
            {
                lines = item.TokenizedFullText;
            }
            else
            {
                lines = Tokenizer.Tokenize(item.FullText);

                item.TokenizedFullText = lines;
            }


            var lineIndex = 0;
            foreach (var line in lines)
            {
                IndexLine(line, lineIndex, primaryKey);

                lineIndex++;
            }
        }


        private IList<string> OrderByFrequency(string query)
        {
            var tokens = Tokenizer.TokenizeOneLine(query).Where(t => t.TokenType != CharClass.Symbol);

            var frequencyByToken = new Dictionary<string, int>();

            foreach (var token in tokens)
            {
                var frequency = 0;
                if (PositionsByToken.TryGetValue(token.NormalizedText, out var list)) frequency = list.Count;

                if (frequency != 0) frequencyByToken[token.NormalizedText] = frequency;
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
        private Dictionary<LinePointer, LineResult> Find(string query, int maxResults)
        {
            var sameLineResult = SameLineFind(query);

            var result = sameLineResult;
            // if the original text is available improve score calculation by taking into account order of tokens
            if (LineProvider != null)
            {
                var tokenizedQuery = Tokenizer.Tokenize(new[] { query });

                result = new Dictionary<LinePointer, LineResult>();

                Parallel.ForEach(sameLineResult, r =>
                {
                    var line = LineProvider(r.Key);
                    var multiplier = ComputeBonusIfOrderIsPreserved(tokenizedQuery[0], line);
                    var value = r.Value.Score * multiplier;


                    lock (result)
                    {
                        result[r.Key] = new LineResult { Score = value, TokensFound = r.Value.TokensFound };
                    }

                });

                //sameLineResult = sameLineResult.ToDictionary(r => r.Key, r =>
                //{
                //    var line = LineProvider(r.Key);
                //    var multiplier = ComputeBonusIfOrderIsPreserved(tokenizedQuery[0], line);
                //    return r.Value * multiplier;
                //});
            }


            return result;
        }



        /// <summary>
        /// Compute the aggregated score of a document from the scores of its lines
        /// </summary>
        static double ComputeDocumentScore(IList<LineResult> lineResults)
        {
            // first sum-up the line scores
            var score = lineResults.Sum(r => r.Score);

            var totalTokens = new HashSet<string>(lineResults.SelectMany(r => r.TokensFound));

            score *= totalTokens.Count * TokenCoverageMultiplier;

            return score;

        }

        /// <summary>
        ///     Find the documents with the highest score (a document score is the sum of its line's scores)
        /// </summary>
        /// <param name="query"></param>
        /// <param name="maxResults"></param>
        /// <returns></returns>
        public IList<SearchResult> SearchBestDocuments(string query, int maxResults)
        {
            var documents = Find(query, maxResults)
                .GroupBy(p => p.Key.PrimaryKey)
                .Select(g => new SearchResult
                {
                    PrimaryKey = g.Key,
                    Score = ComputeDocumentScore(g.Select(p => p.Value).ToList()),
                    LinePointers = g.OrderBy(p => p.Key.Line).Select(p => p.Key).ToList()
                })
                .OrderByDescending(d => d.Score).ToList();

            if (documents.Count > 0)
            {
                var maxScore = documents.Max(d => d.Score);
                var mostInterestingOnes = documents.Where(d => d.Score >= maxScore / 100);
                if (maxResults > 0) return mostInterestingOnes.Take(maxResults).ToList();

                return mostInterestingOnes.ToList();
            }


            return documents;
        }


        /// <summary>
        ///     Search strategy that favors the lines containing more than one token from the query
        /// </summary>
        /// <param name="query"></param>
        /// <returns>score for each found pointer</returns>
        private Dictionary<LinePointer, LineResult> SameLineFind(string query)
        {
            var orderedTokens = OrderByFrequency(query);

            if (orderedTokens.Count == 0)
                // none of the tokens in the query was found
                return new Dictionary<LinePointer, LineResult>();


            var result = new Dictionary<LinePointer, LineResult>();


            Trace?.Trace("Same line strategy");

            var scores = new ScoreByPointer();

            var foundTokensByLine = new Dictionary<LinePointer, List<string>>();

            foreach (var tk in orderedTokens)
            {
                var positions = PositionsByToken[tk];

                foreach (var position in positions.Where(p => p.Deleted == false))
                {
                    if (!foundTokensByLine.TryGetValue(position, out var tokens))
                    {
                        tokens = new List<string>();
                        foundTokensByLine[position] = tokens;
                    }

                    tokens.Add(tk);
                }

                if (positions.Count != 0)
                {
                    // a measure of the information content
                    var score = Math.Log10((double)Entries / positions.Count);
                    foreach (var pointer in positions)
                        scores[pointer] += score;
                }
            }


            foreach (var pair in foundTokensByLine)
            {
                // the multiplier applies only if more than one token in the query was found on the same line
                var score = scores[pair.Key];
                if (pair.Value.Count > 1) score *= SameLineMultiplier * pair.Value.Count;
                result[pair.Key] = new LineResult { Score = scores[pair.Key] = score, TokensFound = pair.Value };
            }

            return result;

        }


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
    }
}