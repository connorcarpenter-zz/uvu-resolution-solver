using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Resolution
{
    public enum TokenType
    {
        String, Period, QuestionMark, ParenBegin, ParenEnd, Comma, Or, Negate
    };

    public class Token
    {
        public TokenType Type;
        public string Name;

        public Token(TokenType type, string name)
        {
            Type = type;
            Name = name;
        }
    }

    class Parser
    {
        private static readonly Dictionary<TokenType, string> TokenRegex = new Dictionary<TokenType, string>()
        {
            {TokenType.Comma, @"^(,)"},
            {TokenType.Negate, @"^(~)"},
            {TokenType.Or, @"^(\|)"},
            {TokenType.ParenBegin, @"^(\()"},
            {TokenType.ParenEnd, @"^(\))"},
            {TokenType.Period, @"^(\.)"},
            {TokenType.QuestionMark, @"^(\?)"},
            {TokenType.String, @"^([a-zA-Z]+)"}
        };

        static public List<Token> Tokenize(string input)
        {
            var output = new List<Token>();

            while (input.Length > 0)
            {
                foreach (var keyValuePair in TokenRegex)
                {
                    var match = Regex.Match(input, keyValuePair.Value);
                    if (!match.Success || match.Value.Length <= 0) continue;
                    output.Add(new Token(keyValuePair.Key, match.Value));
                    input = input.Remove(0, match.Value.Length);
                }
            }

            return output;
        }

        static public RuleData ToRules(List<Token> input)
        {
            var output = new RuleData();
            var currentTerm = new Term();
            output.Terms.Add(currentTerm);
            currentTerm.Id = output.Terms.Count;

            while (input.Count > 0)
            {
                var currentAtom = new Atom();
                currentTerm.Atoms.Add(currentAtom);

                //negativeness
                if (input[0].Type == TokenType.Negate)
                {
                    currentAtom.Truthfulness = false;
                    TokenPop(input);
                }

                //name
                var nameToken = TokenPop(input);
                Expect(nameToken, TokenType.String);
                currentAtom.Name = nameToken.Name;

                //arguments
                Expect(TokenPop(input), TokenType.ParenBegin);
                GetArguments(input, currentAtom.Arguments);

                var orOrPeriodOrQuestionMark = TokenPop(input);
                switch (orOrPeriodOrQuestionMark.Type)
                {
                    case TokenType.Or:
                        continue;
                    case TokenType.Period:
                        if (input.Count > 0)
                        {
                            currentTerm = new Term();
                            output.Terms.Add(currentTerm);
                            currentTerm.Id = output.Terms.Count;
                        }
                        continue;
                    case TokenType.QuestionMark:
                        output.Terms.Remove(currentTerm);
                        output.Goal = currentTerm.Atoms[0];
                        if (input.Count > 0)
                        {
                            currentTerm = new Term();
                            output.Terms.Add(currentTerm);
                            currentTerm.Id = output.Terms.Count;
                        }
                        continue;
                }
                throw new Exception("Not the right input: " + orOrPeriodOrQuestionMark.Name);
            }

            return output;
        }

        private static void GetArguments(List<Token> input, List<Argument> arguments)
        {
            while (true)
            {
                Argument currentArg;

                //name
                var argToken = TokenPop(input);
                Expect(argToken, TokenType.String);

                var commaOrParen = TokenPop(input);
                if (commaOrParen.Type == TokenType.ParenBegin)
                {
                    //is a function
                    var currentFunc = new Argument(argToken.Name, ArgType.Function);
                    GetArguments(input, currentFunc.Arguments);
                    commaOrParen = TokenPop(input);
                    currentArg = currentFunc;
                }
                else
                {
                    //not a function
                    currentArg = new Argument(argToken.Name,
                        char.IsUpper(argToken.Name[0]) ? ArgType.Constant : ArgType.Variable);
                }
                arguments.Add(currentArg);
                if (commaOrParen.Type == TokenType.Comma)
                    continue;
                if (commaOrParen.Type == TokenType.ParenEnd)
                    break;
                throw new Exception("Not the right input: " + commaOrParen.Name);
            }
        }

        static private Token TokenPop(List<Token> input)
        {
            var currentToken = input[0];
            input.RemoveAt(0);
            //Console.WriteLine(currentToken.Name);
            return currentToken;
        }

        static private void Expect(Token token, TokenType type)
        {
            if(token.Type != type)
                throw new Exception("Not the right input: "+token.Name);
        }
    }
}
