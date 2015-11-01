using System;
using System.IO;

namespace Resolution
{
    class Program
    {
        static public bool DEBUG = false;
        static void Main(string[] args)
        {
            string filePath;
            if (args.Length == 0)
            {
                filePath = "../../input.txt";
                DEBUG = true;
            }
            else
                filePath = args[0];

            var rules = GetRules(filePath);
            var result = ResolveGoal(rules);

            if (DEBUG) Console.ReadLine();
        }

        private static bool ResolveGoal(RuleData rules)
        {
            Resolver.Resolve(rules);
            return true;
        }

        static public RuleData GetRules(string filePath)
        {
            try
            {
                var sr = new StreamReader(filePath);
                var input = sr.ReadToEnd();
                input = input.Replace("\r\n", "").Replace(" ", "");
                var tokens = Parser.Tokenize(input);
                var output = Parser.ToRules(tokens);
                return output;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.ReadLine();
                return null;
            }
        }
    }
}
