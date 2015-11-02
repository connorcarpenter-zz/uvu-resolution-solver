using System;
using System.Collections.Generic;
using System.Linq;

namespace Resolution
{
    
    class Resolver
    {
        private static readonly TermComparer termComparer = new TermComparer();

        public static void Resolve(RuleData rules)
        {
            Console.WriteLine("Input clauses: ");
            rules.Print();
            Console.WriteLine("\nQuery:\n     " + rules.Goal.Stringify() + "?\n");
            
            var negatedGoal = new Atom(rules.Goal);
            negatedGoal.Truthfulness = !negatedGoal.Truthfulness;
            var negatedGoalTerm = new Term(negatedGoal) {FromGoal = 0};
            var newRules = new RuleData();
            newRules.Terms.Add(negatedGoalTerm);
            rules.Terms.Add(negatedGoalTerm);
            negatedGoalTerm.Id = rules.Terms.Count;
            var initialRulesLength = rules.Terms.Count;

            var newTerms = GetInitialNewTerms(rules);
            while (true)
            {
                if (newTerms.Count == 0)
                {
                    Console.WriteLine("Result: FALSE. No solution.");
                    break;
                }
                newTerms.Sort(termComparer);
                var topTerm = newTerms[0];
                newTerms.Remove(topTerm);
                newRules.Terms.Add(topTerm);
                rules.Terms.Add(topTerm);
                topTerm.Id = rules.Terms.Count;
                if (topTerm.Atoms[0].IsBool && !topTerm.Atoms[0].Truthfulness)
                {
                    Console.WriteLine("Result: TRUE.\n\nProof clauses added:");
                    newRules = newRules.Prune(initialRulesLength);
                    newRules.Print();
                    break;
                }
                var newestTerms = GetNewTerms(topTerm, rules, newTerms);
                newTerms.AddRange(newestTerms);
            }
        }

        public static List<Term> GetInitialNewTerms(RuleData rules)
        {
            var output = new List<Term>();

            for (var i = 0; i < rules.Terms.Count; i++)
            {
                for (var j = i + 1; j < rules.Terms.Count; j++)
                {
                    var combinedTerm = CombineTerms(rules.Terms[i], rules.Terms[j]);
                    if(!combinedTerm.SubMap.Failure && !combinedTerm.ExistsInList(rules.Terms))
                        output.Add(combinedTerm);
                }
            }

            return output;
        }

        public static List<Term> GetNewTerms(Term newestTerm, RuleData rules, List<Term> newTerms)
        {
            var output = new List<Term>();

            for (var i = 0; i < rules.Terms.Count - 1; i++)
            {
                var combinedTerm = CombineTerms(rules.Terms[i], newestTerm);
                if (!combinedTerm.SubMap.Failure &&
                    !combinedTerm.ExistsInList(rules.Terms) &&
                    !combinedTerm.ExistsInList(newTerms))
                    output.Add(combinedTerm);
            }

            return output;
        }

        private static Term CombineTerms(Term a, Term b)
        {
            var output = new Term
            {
                ParentA = a.Id,
                ParentB = b.Id
            };
            var termA = new Term(a);
            var termB = new Term(b);

            //unify submaps of each term
            output.SubMap = new SubstitutionMap();//UnifySubMap(termA.SubMap, termB.SubMap);
            if (output.SubMap.Failure)
                return output;

            //add goal descendent heuristic
            output.FromGoal = Math.Min(termA.FromGoal, termB.FromGoal);
            if (output.FromGoal != 100) output.FromGoal += 1;

            //match each atom in each term
            var combinedAtoms = GetCombinedAtoms(termA, termB, output.SubMap);
            if (combinedAtoms.Any(atom => atom.IsBool && atom.Truthfulness))
            {
                output.SubMap.Failure = true;
                return output;
            }
            combinedAtoms = CleanFalses(combinedAtoms);
            output.Atoms.AddRange(combinedAtoms);

            //evaluate heuristic
            output.Heuristic = GetHeuristic(output);

            return output;
        }

        private static List<Atom> CleanFalses(IReadOnlyList<Atom> atoms)
        {
            var allFalses = !atoms.Any(atom => !atom.IsBool || (atom.IsBool && atom.Truthfulness));
            return allFalses ?
                new List<Atom> { atoms[0] } :
                atoms.Where(atom => !atom.IsBool || (atom.IsBool && atom.Truthfulness)).ToList();
        }

        private static List<Atom> GetCombinedAtoms(Term a, Term b, SubstitutionMap subMap)
        {
            //apply combined submap to each term
            var termA = new Term(a);
            var termB = new Term(b);
            termA.ApplySubMap(subMap);
            termB.ApplySubMap(subMap);
            var output = new List<Atom>();
            
            //add all of term A's atoms if they check out
            var allFailed = true;
            foreach (var atomA in termA.Atoms)
            {
                var atomToAdd = atomA;
                foreach (var atomB in termB.Atoms)
                {
                    var unifyResult = Unify(atomA, atomB, subMap);
                    if (unifyResult.Failure) continue;
                    allFailed = false;
                    if (unifyResult.Changed)
                    {
                        return GetCombinedAtoms(termA, termB, unifyResult);
                    }
                    atomToAdd = new Atom
                    {
                        Name = "",
                        Arguments = null,
                        IsBool = true,
                        Truthfulness = (atomA.Truthfulness == atomB.Truthfulness)
                    };
                    break;
                }
                output.Add(atomToAdd);
                if (atomToAdd.IsBool && atomToAdd.Truthfulness)
                    return output;
            }
            //add all of term B's atoms if they check out
            foreach (var atomB in termB.Atoms)
            {
                var atomToAdd = atomB;
                foreach (var atomA in termA.Atoms)
                {
                    var unifyResult = Unify(atomB, atomA, subMap);
                    if (unifyResult.Failure) continue;
                    allFailed = false;
                    if (unifyResult.Changed)
                    {
                        return GetCombinedAtoms(termB, termA, unifyResult);
                    }
                    atomToAdd = new Atom
                    {
                        Name = "",
                        Arguments = null,
                        IsBool = true,
                        Truthfulness = (atomA.Truthfulness == atomB.Truthfulness)
                    };
                    break;
                }
                output.Add(atomToAdd);
                if (atomToAdd.IsBool && atomToAdd.Truthfulness)
                    return output;
            }
            if (allFailed)
            {
                return new List<Atom> {
                    new Atom
                    {
                        IsBool = true,
                        Truthfulness = true
                    }
                };
            }
            return output;
        }

        private static SubstitutionMap Unify(Atom a, Atom b, SubstitutionMap subMap)
        {
            var output = new SubstitutionMap(subMap);

            if (a.Name != b.Name || a.Arguments.Count != b.Arguments.Count)
            {
                output.Failure = true;
                return output;
            }

            var atomA = new Atom(a);
            var atomB = new Atom(b);

            atomA.ApplySubMap(output);
            atomB.ApplySubMap(output);

            var disagreement = false;
            for (var i = 0; i < atomA.Arguments.Count; i++)
            {
                var argA = atomA.Arguments[i];
                var argB = atomB.Arguments[i];
                if (argA.EqualTo(argB)) continue;

                disagreement = true;
                if (argA.CanMapTo(argB))
                {
                    subMap.Add(argA, argB);
                    var newOutput = Unify(atomA, atomB, subMap);
                    newOutput.Changed = true;
                    return newOutput;
                }
                if (argB.CanMapTo(argA))
                {
                    subMap.Add(argB, argA);
                    var newOutput = Unify(atomA, atomB, subMap);
                    newOutput.Changed = true;
                    return newOutput;
                }
                break;
            }
            if (disagreement)
            {
                output.Failure = true;
            }
            return output;
        }

        private static bool ArgNotInFunc(Argument arg, Argument func)
        {
            if (arg.Type != ArgType.Variable) return true;
            return func.Type != ArgType.Function || func.Arguments.All(subArg => !subArg.EqualTo(arg));
        }

        private static SubstitutionMap UnifySubMap(SubstitutionMap subMapA, SubstitutionMap subMapB)
        {
            var output = new SubstitutionMap();
            foreach (var keyA in subMapA.Keys)
            {
                if (subMapB.ContainsKey(keyA))
                {
                    output.Failure = true;
                    return output;
                }
                output.Add(keyA, subMapA.Get(keyA));
            }
            foreach (var keyB in subMapB.Keys)
                output.Add(keyB, subMapB.Get(keyB));
            
            return output;
        }

        private static int GetHeuristic(Term term)
        {
            var output = term.FromGoal;
            output += term.Atoms.Count;
            if (term.Atoms[0].IsBool && !term.Atoms[0].Truthfulness)
                output -= 1000000;
            return output;
        }
    }

    class TermComparer : IComparer<Term>
    {
        public int Compare(Term x, Term y)
        {
            return x.Heuristic - y.Heuristic;
        }
    }
}
