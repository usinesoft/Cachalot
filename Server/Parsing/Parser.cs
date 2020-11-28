using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace Server.Parsing
{
    public class Parser
    {
        public Node ParseSql(string line)
        {
            var tokens = Tokenizer.TokenizeOneLine(line);

            if (tokens.FirstOrDefault().Is("select")) return ParseSelect(tokens.TrimLeft("select"));

            return new Node {ErrorMessage = "select keyword not found"};
        }

        private Node ParseSelect(IList<Token> tokens)
        {
            var result = new Node {Token = "select"};

            // ignore "*" and "from" if present
            tokens = tokens.TrimLeft("*", "from");


            var parts = tokens.Split("where");

            var take = tokens.Split("take");

            bool hasTakeCause = false;

            if (take.Count == 2) // a take close is present
            {
                hasTakeCause = true;
                if (take[1].Count > 0)
                {
                    
                    var takeCount = take[1][0].NormalizedText;
                    if (int.TryParse(takeCount, out var _))
                    {
                        var takeNode = new Node{Token = "take"};
                        takeNode.Children.Add(new Node{Token = takeCount });
                        result.Children.Add(takeNode);
                    }
                    else
                    {
                        result.ErrorMessage = "error in take clause";
                        return result;
                    }
                }

                else
                {
                    result.ErrorMessage = "error in take clause";
                    return result;
                }
            }


            // no where clause
            if (parts.Count == 1 && parts[0].Count == 1)
            {
                result.Children.Add(new Node {Token = parts[0][0].NormalizedText});
                return result;
            }

            if (parts.Count != 2 || parts[0].Count != 1)
            {
                result.ErrorMessage = "invalid syntax: should be select table where condition";
            }
            else
            {
                result.Children.Add(new Node {Token = parts[0][0].NormalizedText});

                var afterWhere = parts[1];
                if (hasTakeCause)
                {
                    afterWhere = parts[1].Split("take")[0];
                }

                result.Children.Add(ParseWhere(afterWhere));
            }


            return result;
        }

        private Node ParseWhere(IList<Token> tokens)
        {
            var result = new Node {Token = "where"};

            var parts = tokens.Split("or");

            var orNode = new Node {Token = "or"};

            result.Children.Add(orNode);

            foreach (var part in parts) orNode.Children.Add(ParseAnd(part));

            return result;
        }

        private Node ParseAnd(IList<Token> tokens)
        {
            var result = new Node {Token = "and"};

            var parts = tokens.Split("and");

            foreach (var part in parts) result.Children.Add(ParseExpression(part));

            return result;
        }

        private string TryNormalizeSymbol(string symbol)
        {
            switch (symbol)
            {
                case "==":
                case "=":
                    return "=";

                case "!=":
                case "<>":
                    return "!=";

                case "<":
                    return "<";

                case "<=":
                    return "<=";

                case ">":
                    return ">";

                case ">=":
                    return ">=";
            }


            return null;
        }

        private string TryNormalizeValue(string value)
        {
            value = value.Trim('\'', '"');

            var commas = value.Count(c => c == ',');

            if (commas > 1)
                return null;

            if (commas == 1)
                value = value.Replace(',', '.');

            return value;
        }

        private Node ParseExpression(IList<Token> tokens)
        {
            if (tokens.Count > 2 )
            {
                
                var column = tokens[0].NormalizedText;

                // simple expression like column operator value
                if (tokens[1].TokenType == CharClass.Symbol)
                {
                    var symbol = TryNormalizeSymbol(tokens[1].NormalizedText);

                    
                    if (symbol != null)
                    {
                        var result = new Node {Token = symbol};

                        // column name
                        result.Children.Add(new Node {Token = column});

                        // value
                        var value = tokens.Join(2);

                        var normalized = TryNormalizeValue(value);

                        if (normalized != null)
                            result.Children.Add(new Node {Token = normalized});
                        else
                            result.ErrorMessage = $"can not parse value {value}";


                        return result;
                    }
                }
                
                // in (value1, value2)
                if(tokens[1].Is("in")) 
                {
                    var result = new Node {Token = "in"};
                    // column name
                    result.Children.Add(new Node {Token = column});

                    var tokensLit = tokens.Skip(2).TrimLeft("(").TrimRight(")").Split(",");

                    foreach (var tks in tokensLit)
                    {
                        var value = tks.Join();
                        var normalized = TryNormalizeValue(value);

                        if (normalized != null)
                        {
                            result.Children.Add(new Node {Token = normalized});
                        }

                        else result.ErrorMessage = $"can ot parse value {value} in IN clause";


                    }

                    return result;

                }


                // tags contains 'geek'
                if (tokens[1].Is("contains"))
                {
                    var result = new Node {Token = "contains"};
                    // column name
                    result.Children.Add(new Node {Token = column});

                    var value = tokens.Skip(2).Join();

                    var normalized = TryNormalizeValue(value);

                    if (normalized != null)
                    {
                        result.Children.Add(new Node {Token = normalized});
                    }

                    else result.ErrorMessage = $"can not parse value {value} in CONTAINS clause";

                    return result;
                }

                if (tokens.Count > 3)
                {

                    // not in (value1, value2)
                    if (tokens[1].Is("not") && tokens[2].Is("in"))
                    {
                        var result = new Node {Token = "not in"};
                        // column name
                        result.Children.Add(new Node {Token = column});

                        var tokensLit = tokens.Skip(3).TrimLeft("(").TrimRight(")").Split(",");

                        foreach (var tks in tokensLit)
                        {
                            var value = tks.Join();
                            var normalized = TryNormalizeValue(value);

                            if (normalized != null)
                            {
                                result.Children.Add(new Node {Token = normalized});
                            }

                            else result.ErrorMessage = $"can ot parse value {value} in IN clause";


                        }

                        return result;

                    }

                    // not contains (value1, value2)
                    if (tokens[1].Is("not") && tokens[2].Is("contains"))
                    {
                        var result = new Node {Token = "not contains"};
                        // column name
                        result.Children.Add(new Node {Token = column});

                        var tokensLit = tokens.Skip(3).TrimLeft("(").TrimRight(")").Split(",");

                        foreach (var tks in tokensLit)
                        {
                            var value = tks.Join();
                            var normalized = TryNormalizeValue(value);

                            if (normalized != null)
                            {
                                result.Children.Add(new Node {Token = normalized});
                            }

                            else result.ErrorMessage = $"can ot parse value {value} in IN clause";


                        }

                        return result;

                    }
                }

                return new Node {ErrorMessage = $"Can not parse symbol {tokens[1].NormalizedText}"};
            }

            return new Node {Token = tokens[0].NormalizedText};
        }
    }
}