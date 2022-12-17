using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Client.Parsing
{
    public static class Tokenizer
    {
        private const string EscapedQuoteReplacement = "(#)";

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static CharClass GetCharClass(char ch)
        {
            if (char.IsWhiteSpace(ch)) return CharClass.Whitespace;

           
            if ("<>:=,()!".Contains(ch))
                return CharClass.Symbol;
            
            return CharClass.LetterOrDigit;
        }

        public static IList<Token> TokenizeOneLine(string input)
        {
            var result = new List<Token>(1000);

            input = input.Trim().Replace("\\'", EscapedQuoteReplacement);

            var previousSplitPosition = 0;
            var previousType = CharClass.Start;

            bool insideString = false;

            for (var i = 0; i < input.Length; i++)
            {
                var current = input[i];

                var currentType = GetCharClass(current);

                if (current == '\'') // string delimiter
                {
                    if (insideString)
                    {
                        var text = input.Substring(previousSplitPosition, i - previousSplitPosition +1);

                        var txt = text.Replace(EscapedQuoteReplacement, "'");
                        result.Add(new Token
                        {
                            // restore escaped quotes id any
                            NormalizedText = txt.ToLower(),
                            Text = txt,
                            TokenType = previousType
                        });

                        previousSplitPosition = i +1;
                        
                    }
                    else
                    {
                        if (previousType != CharClass.Whitespace && previousType != CharClass.Start)
                        {
                            var text = input.Substring(previousSplitPosition, i - previousSplitPosition);
                            
                            result.Add(new Token
                            {
                                NormalizedText = text.ToLower(),
                                Text = text,
                                TokenType = previousType
                            });
                        
                            
                        }

                        previousType = currentType;

                        previousSplitPosition = i;
                    }
                    
                    insideString = !insideString;
                }
                else
                {
                    if (currentType != previousType && !insideString)
                    {
                        if (previousType != CharClass.Whitespace && previousType != CharClass.Start)
                        {
                            var text = input.Substring(previousSplitPosition, i - previousSplitPosition);

                            if (text.Length > 0)
                            {
                                result.Add(new Token
                                {
                                    NormalizedText = text.ToLower(),
                                    Text = text,
                                    TokenType = previousType
                                });
                            }
                        }

                        previousType = currentType;

                        previousSplitPosition = i;
                    }
                }
                
            }

            if (previousType != CharClass.Whitespace)
            {
                var text = input.Substring(previousSplitPosition, input.Length - previousSplitPosition);

                if (text.Length > 0)
                {
                    result.Add(new()
                    {
                        NormalizedText = text.ToLower(),
                        Text = text,
                        TokenType = previousType
                    });
                }
                
            }


            return result;
        }
    }
}