using System.Collections.Generic;
using System.Linq;

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
            // a complete result will have the following children in this order: distinct, projection, table, where, order, take, skip
            var result = new Node {Token = "select"};

            var partsBySeparator = tokens.Split("from", "where", "take", "order");


            // parse the projection (can be nothing or * (the same result), ~ = all the server-side values, or an explicit list of properties with optional aliases
            
            var projectionNode = new Node{Token = "projection"};
            result.Children.Add(projectionNode);

            if (partsBySeparator.TryGetValue("", out var beforeFrom))
            {
                // * is optional
                if (beforeFrom.Count == 0)
                {
                    projectionNode.Children.Add(new Node{Token = "*"});
                }
                else if (beforeFrom.Count == 1) // can be *, ~ or property name
                {
                    var value = beforeFrom[0];
                    projectionNode.Children.Add(new Node{Token = value.NormalizedText});

                }
                else // multiple elements like property1 alias1, property2 alias 2
                {
                    var parts = beforeFrom.Split(",");

                    foreach (var part in parts)
                    {
                        var name = part[0];

                        var node = new Node {Token = name.NormalizedText};

                        if (part.Count == 2)
                        {
                            var alias = part[1].NormalizedText;
                            node.Children.Add(new Node{Token = alias});
                        }

                        
                        projectionNode.Children.Add(node);
                    }
                }
            }

            // the collection name
            if (partsBySeparator.TryGetValue("from", out var afterFrom))
            {
                if (afterFrom.Count != 1)
                {
                    result.ErrorMessage = "A single collection(table) name must be specified";
                }
                else
                {
                    result.Children.Add(new Node{Token = "from", Children = { new Node{Token = afterFrom[0].NormalizedText}}});
                }
            }

            // the where clause
            if (partsBySeparator.TryGetValue("where", out var afterWhere))
            {
                if (afterWhere.Count == 0)
                {
                    result.ErrorMessage = "Invalid where clause";
                }
                else
                {
                    result.Children.Add(ParseWhere(afterWhere));
                }
                
            }

            // parse the "take" clause
            if (partsBySeparator.TryGetValue("take", out var afterTake)) // a take clause is present
            {
                
                if (afterTake.Count == 1) // oly one number can be preset after tha take clause
                {
                    
                    var takeCount = afterTake[0].NormalizedText;
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

                // name like 'john%'
                if (tokens[1].Is("like"))
                {
                    var result = new Node {Token = "like"};
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