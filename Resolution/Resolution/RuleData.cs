using System;
using System.Collections.Generic;
using System.Linq;

namespace Resolution
{
    public class RuleData
    {
        public List<Term> Terms;
        public Atom Goal;

        public RuleData()
        {
            Terms = new List<Term>();
        }

        public void Print()
        {
            foreach (var term in Terms)
            {
                Console.WriteLine(term.Stringify());
            }
        }

        public RuleData Prune(int rulesLength)
        {
            var output = new RuleData();
            PruneAddParents(output.Terms, Terms.Last());
            foreach(var term in output.Terms)
                PruneRenumber(output.Terms, term, rulesLength);
            return output;
        }

        private static void PruneRenumber(List<Term> terms, Term last, int rulesLength)
        {
            var oldId = last.Id;
            var newId = terms.FindIndex(t => t == last)+rulesLength;
            if (oldId == newId) return;
            last.Id = newId;
            foreach (var term in terms.Where(term => term != last))
            {
                if(term.Id == newId)
                    PruneRenumber(terms, term, rulesLength);
                if (term.ParentA == oldId)
                    term.ParentA = newId;
                if (term.ParentB == oldId)
                    term.ParentB = newId;
            }
        }

        private void PruneAddParents(IList<Term> terms, Term lastTerm)
        {
            while (true)
            {
                if (terms.Contains(lastTerm)) return;
                terms.Insert(0, lastTerm);
                var parentA = FindTerm(lastTerm.ParentA);
                var parentB = FindTerm(lastTerm.ParentB);
                if (parentB != null) PruneAddParents(terms, parentB);
                if (parentA != null)
                {
                    lastTerm = parentA;
                    continue;
                }
                break;
            }
        }

        private Term FindTerm(int index)
        {
            return Terms.FirstOrDefault(term => term.Id == index);
        }
    }

    public class SubstitutionMap
    {
        private readonly Dictionary<Argument, Argument> _vars;
        public bool Failure = false;
        public bool Changed = false;

        public SubstitutionMap()
        {
            _vars = new Dictionary<Argument, Argument>();
        }

        public SubstitutionMap(SubstitutionMap subMap)
        {
            if (subMap._vars.Count > 0)
            {
                _vars = new Dictionary<Argument, Argument>();
                foreach (var key in subMap.Keys)
                {
                    _vars.Add(new Argument(key), new Argument(subMap.Get(key)));
                }
            }
            else
            {
                _vars = new Dictionary<Argument, Argument>();
            }
        }

        public IEnumerable<Argument> Keys => _vars.Keys;

        public Argument Get(Argument argument)
        {
            return (from key in _vars.Keys where key.EqualTo(argument) select _vars[key]).FirstOrDefault();
        }

        public bool ContainsKey(Argument argument)
        {
            return _vars.Keys.Any(key => key.EqualTo(argument));
        }

        public void Add(Argument argKey, Argument argValue)
        {
            if(argKey.Type == ArgType.Constant)
                throw new Exception("Can't substitute a constant for another argument");
            if(!ContainsKey(argKey))
                _vars.Add(new Argument(argKey), new Argument(argValue));
        }

        public bool EqualTo(SubstitutionMap subMap)
        {
            if (Keys.Count() != subMap.Keys.Count()) return false;
            foreach (var mySubs in _vars)
            {
                if (!subMap.ContainsKey(mySubs.Key)) return false;
                if (!subMap.Get(mySubs.Key).EqualTo(Get(mySubs.Key))) return false;
            }
            return true;
        }
    }

    public class Term
    {
        public List<Atom> Atoms;
        public SubstitutionMap SubMap; 
        public int FromGoal = 100;
        public int Heuristic { get; set; }
        public int Id { get; set; }
        public int ParentB { get; set; }
        public int ParentA { get; set; }

        public Term()
        {
            Init();
        }

        public Term(Atom atom)
        {
            Init();
            Atoms.Add(atom);
        }

        public Term(Term term)
        {
            Id = term.Id;
            ParentA = term.ParentA;
            ParentB = term.ParentB;
            Atoms = new List<Atom>();
            foreach (var atom in term.Atoms)
                Atoms.Add(new Atom(atom));
            SubMap = new SubstitutionMap(term.SubMap);
            FromGoal = term.FromGoal;
        }

        private void Init()
        {
            Atoms = new List<Atom>();
            SubMap = new SubstitutionMap();
        }

        public void ApplySubMap(SubstitutionMap subMap)
        {
            foreach(var atom in Atoms)
                atom.ApplySubMap(subMap);
        }

        public string Stringify()
        {
            var parentString = "";
            if(ParentA != 0 && ParentB != 0)
                parentString = "" + ParentA + " + " + ParentB + " = ";
            parentString += "" + Id + ". ";
            var output = new string(' ', 15 - parentString.Length);
            output += parentString;
            var addedAtom = false;
            foreach (var atom in Atoms)
            {
                if (addedAtom)
                    output += " | ";
                addedAtom = true;
                output += atom.Stringify();
            }
            output += ".";
            return output;
        }

        public bool ExistsInList(List<Term> terms)
        {
            return terms.Any(EqualTo);
        }

        private bool EqualTo(Term otherTerm)
        {
            var all = otherTerm.Atoms.Select(otherAtom => Atoms.Any(otherAtom.EqualTo)).All(inOtherAtomList => inOtherAtomList);
            var any = Atoms.Select(atom => otherTerm.Atoms.Any(atom.EqualTo)).Any(inOtherAtomList => !inOtherAtomList);
            var atomsAreEqual = !any && all;

            var subMapsAreEqual = SubMap.EqualTo(otherTerm.SubMap);
            return atomsAreEqual && subMapsAreEqual;
        }
    }

    public class Atom
    {
        public bool Truthfulness;
        public string Name;
        public List<Argument> Arguments;
        public bool IsBool = false;

        public Atom()
        {
            Arguments = new List<Argument>();
            Truthfulness = true;
        }

        public Atom(Atom copyAtoms)
        {
            Arguments = new List<Argument>();
            foreach (var arg in copyAtoms.Arguments)
            {
                Arguments.Add(new Argument(arg));
            }
            Truthfulness = copyAtoms.Truthfulness;
            Name = copyAtoms.Name;
        }

        public void ApplySubMap(SubstitutionMap subMap)
        {
            InnerLoop(Arguments, subMap);
        }

        public void InnerLoop(List<Argument> arguments, SubstitutionMap subMap)
        {
            foreach (var arg in arguments)
            {
                switch (arg.Type)
                {
                    case ArgType.Constant:
                        continue;
                    case ArgType.Variable:
                        if (subMap.ContainsKey(arg))
                        {
                            var otherArg = subMap.Get(arg);
                            arg.Name = otherArg.Name;
                            arg.Type = otherArg.Type;
                        }
                        continue;
                    case ArgType.Function:
                        if (subMap.ContainsKey(arg))
                        {
                            var otherArg = new Argument(subMap.Get(arg));
                            arg.Name = otherArg.Name;
                            arg.Type = otherArg.Type;
                            arg.Arguments = otherArg.Arguments;
                        }
                        else
                            InnerLoop(arg.Arguments, subMap);
                        continue;
                }
            }
        }

        public string Stringify()
        {
            var output = "";
            if (IsBool)
            {
                return Truthfulness ? "TRUE" : "FALSE";
            }
            if (!Truthfulness) output += "~";
            output += Name;
            output = StringifyArguments(output, Arguments);
            return output;
        }

        private static string StringifyArguments(string output, IEnumerable<Argument> argList)
        {
            output += "(";
            var addedArg = false;
            foreach (var arg in argList)
            {
                if (addedArg)
                    output += ", ";
                addedArg = true;
                output += arg.Name;
                if (arg.Type == ArgType.Function)
                {
                    output = StringifyArguments(output, arg.Arguments);
                }
            }
            output += ")";
            return output;
        }

        public bool EqualTo(Atom otherAtom)
        {
            if (otherAtom.Truthfulness != Truthfulness) return false;
            if (otherAtom.Name != Name) return false;
            if (otherAtom.IsBool != IsBool) return false;
            if (otherAtom.Arguments.Count != Arguments.Count) return false;
            for (var i = 0; i < otherAtom.Arguments.Count; i++)
            {
                var myArg = Arguments[i];
                var otherArg = otherAtom.Arguments[i];
                if (!myArg.EqualTo(otherArg))
                    return false;
            }
            return true;
        }
    }

    public enum ArgType { Constant, Variable, Function };

    public class Argument
    {
        public ArgType Type;
        public string Name;
        public List<Argument> Arguments;

        public bool EqualTo(Argument otherArg)
        {
            if (Type != otherArg.Type) return false;
            if (Name != otherArg.Name) return false;
            if (Type != ArgType.Function) return true;
            if (Arguments.Count != otherArg.Arguments.Count)
                return false;
            return !Arguments.Where((t, i) => !t.EqualTo(otherArg.Arguments[i])).Any();
        }

        public Argument(string name, ArgType type)
        {
            Name = name;
            Type = type;
            Init();
        }

        public Argument(Argument otherArg)
        {
            Type = otherArg.Type;
            Name = otherArg.Name;
            Init();
            if (otherArg.Arguments == null || otherArg.Arguments.Count <= 0) return;
            foreach(var arg in otherArg.Arguments)
                Arguments.Add(new Argument(arg));
        }

        protected void Init()
        {
            if(Type == ArgType.Function)
                Arguments = new List<Argument>();
        }
    }
}
