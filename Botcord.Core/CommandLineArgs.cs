using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using ArrayList = System.Collections.Generic.List<object>;
using ListDictionary = System.Collections.Generic.Dictionary<string, object>;

namespace Botcord.Core
{
    /// <summary>
    /// NOTE: All this has been lifted and modified from Docopt c# library
    /// </summary>

    public class ValueObject
    {
        public object Value { get; private set; }

        internal ValueObject(object obj)
        {
            if (obj is ArrayList)
            {
                Value = new ArrayList(obj as ArrayList);
                return;
            }
            Value = obj;
        }

        internal ValueObject()
        {
            Value = null;
        }

        public bool IsNullOrEmpty
        {
            get { return Value == null || Value.ToString() == ""; }
        }

        public bool IsFalse
        {
            get { return (Value as bool?) == false; }
        }

        public bool IsTrue
        {
            get { return (Value as bool?) == true; }
        }

        public bool IsList
        {
            get { return Value is ArrayList; }
        }

        internal bool IsOfTypeInt
        {
            get { return Value is int?; }
        }

        public bool IsInt
        {
            get
            {
                int value;
                return Value != null && (Value is int || Int32.TryParse(Value.ToString(), out value));
            }
        }

        public int AsInt
        {
            get { return IsList ? 0 : Convert.ToInt32(Value); }
        }

        public bool IsFloat
        {
            get
            {
                float value;
                return Value != null && (Value is float || float.TryParse(Value.ToString(), out value));
            }
        }

        public float AsFloat
        {
            get { return IsList ? 0.0f : (float)Convert.ToDouble(Value); }
        }

        public bool IsLong
        {
            get
            {
                long value;
                return Value != null && Value is long || Int64.TryParse(Value.ToString(), out value);
            }
        }

        public long AsLong
        {
            get { return Convert.ToInt64(Value); }
        }

        public bool IsULong
        {
            get
            {
                ulong value;
                return Value != null && Value is ulong || UInt64.TryParse(Value.ToString(), out value);
            }
        }

        public ulong AsULong
        {
            get { return Convert.ToUInt64(Value); }
        }

        public bool IsString
        {
            get { return Value is string; }
        }

        public override bool Equals(object obj)
        {
            //       
            // See the full list of guidelines at
            //   http://go.microsoft.com/fwlink/?LinkID=85237  
            // and also the guidance for operator== at
            //   http://go.microsoft.com/fwlink/?LinkId=85238
            //

            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            var v = (obj as ValueObject).Value;
            if (Value == null && v == null) return true;
            if (Value == null || v == null) return false;
            if (IsList || (obj as ValueObject).IsList)
                return Value.ToString().Equals(v.ToString());
            return Value.Equals(v);
        }

        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }

        public override string ToString()
        {
            if (IsList)
            {
                var l = (from object v in AsList select v.ToString()).ToList();
                return string.Format("{0}", String.Join(" ", l));
            }
            return (Value ?? "").ToString();
        }

        internal void Add(ValueObject increment)
        {
            if (increment == null) throw new ArgumentNullException("increment");

            if (increment.Value == null) throw new InvalidOperationException("increment.Value is null");

            if (Value == null) throw new InvalidOperationException("Value is null");

            if (increment.IsOfTypeInt)
            {
                if (IsList)
                    (Value as ArrayList).Add(increment.AsInt);
                else
                    Value = increment.AsInt + AsInt;
            }
            else
            {
                var l = new ArrayList();
                if (IsList)
                {
                    l.AddRange(AsList);
                }
                else
                {
                    l.Add(Value);
                }
                if (increment.IsList)
                {
                    l.AddRange(increment.AsList);
                }
                else
                {
                    l.Add(increment);
                }
                Value = l;
            }
        }

        internal void Append(ValueObject increment)
        {
            if (increment == null) throw new ArgumentNullException("increment");

            if (increment.Value == null) throw new InvalidOperationException("increment.Value is null");

            if (Value == null) throw new InvalidOperationException("Value is null");

            var l = new ArrayList();
            if (IsList)
            {
                l.AddRange(AsList);
            }
            else
            {
                l.Add(Value);
            }
            if (increment.IsList)
            {
                l.AddRange(increment.AsList);
            }
            else
            {
                l.Add(increment);
            }
            Value = l;
        }

        public ArrayList AsList
        {
            get { return IsList ? (Value as ArrayList) : (new ArrayList(new[] { Value })); }
        }
    }

    public class SingleMatchResult
    {
        public SingleMatchResult(int index, ArgPattern match)
        {
            Position = index;
            Match = match;
        }

        public SingleMatchResult()
        {
        }

        public int Position { get; set; }
        public ArgPattern Match { get; set; }
    }

    public class MatchResult
    {
        public bool Matched;
        public IList<ArgPattern> Left;
        public IEnumerable<ArgPattern> Collected;

        public MatchResult() { }

        public MatchResult(bool matched, IList<ArgPattern> left, IEnumerable<ArgPattern> collected)
        {
            Matched = matched;
            Left = left;
            Collected = collected;
        }

        public bool LeftIsEmpty { get { return Left.Count == 0; } }

        public override bool Equals(object obj)
        {
            //       
            // See the full list of guidelines at
            //   http://go.microsoft.com/fwlink/?LinkID=85237  
            // and also the guidance for operator== at
            //   http://go.microsoft.com/fwlink/?LinkId=85238
            //

            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            return ToString().Equals(obj.ToString());
        }

        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }

        public override string ToString()
        {
            return string.Format("matched={0} left=[{1}], collected=[{2}]",
                Matched,
                Left == null ? "" : string.Join(", ", Left.Select(p => p.ToString())),
                Collected == null ? "" : string.Join(", ", Collected.Select(p => p.ToString()))
            );
        }
    }

    public class ArgTokens : IEnumerable<string>
    {
        private readonly List<string> _tokens = new List<string>();

        public ArgTokens(IEnumerable<string> source)
        {
            _tokens.AddRange(source);
        }

        public ArgTokens(string source)
        {
            _tokens.AddRange(source.Split(new char[0], StringSplitOptions.RemoveEmptyEntries));
        }

        public static ArgTokens FromPattern(string pattern)
        {
            var spacedOut = Regex.Replace(pattern, @"([\[\]\(\)\|]|\.\.\.)", @" $1 ");
            var source = Regex.Split(spacedOut, @"\s+|(\S*<.*?>)").Where(x => !string.IsNullOrEmpty(x));
            return new ArgTokens(source);
        }

        public IEnumerator<string> GetEnumerator()
        {
            return _tokens.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public string Move()
        {
            string s = null;
            if (_tokens.Count > 0)
            {
                s = _tokens[0];
                _tokens.RemoveAt(0);
            }
            return s;
        }

        public string Current()
        {
            return (_tokens.Count > 0) ? _tokens[0] : null;
        }

        public override string ToString()
        {
            return string.Format("current={0},count={1}", Current(), _tokens.Count);
        }
    }

    public class ArgPattern
    {
        public ValueObject Value { get; set; }

        public virtual string Name
        {
            get { return ToString(); }
        }

        // override object.Equals
        public override bool Equals(object obj)
        {
            //       
            // See the full list of guidelines at
            //   http://go.microsoft.com/fwlink/?LinkID=85237  
            // and also the guidance for operator== at
            //   http://go.microsoft.com/fwlink/?LinkId=85238
            //

            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            return ToString() == obj.ToString();
        }

        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }

        public virtual bool HasChildren
        {
            get { return Children != null && Children.Count() > 0; }
        }

        public IList<ArgPattern> Children { get; set; }

        public virtual MatchResult Match(IList<ArgPattern> left, IEnumerable<ArgPattern> collected = null)
        {
            return new MatchResult();
        }
    }

    public class ArgBranchPattern : ArgPattern
    {

        public ArgBranchPattern(params ArgPattern[] children)
        {
            if (children == null) throw new ArgumentNullException("children");
            Children = children;
        }

        public override bool HasChildren { get { return true; } }

        /*public IEnumerable<Pattern> Flat<T>() where T : Pattern
        {
            return Flat(typeof(T));
        }

        public override ICollection<Pattern> Flat(params Type[] types)
        {
            if (types == null) throw new ArgumentNullException("types");
            if (types.Contains(this.GetType()))
            {
                return new Pattern[] { this };
            }
            return Children.SelectMany(child => child.Flat(types)).ToList();
        */

        public override string ToString()
        {
            return string.Format("{0}({1})", GetType().Name, String.Join(", ", Children.Select(c => c == null ? "None" : c.ToString())));
        }
    }

    public class ArgLeafPattern : ArgPattern
    {
        private readonly string _name;

        protected ArgLeafPattern(string name, ValueObject value = null)
        {
            _name = name;
            Value = value;
        }

        protected ArgLeafPattern()
        {
        }

        public override string Name
        {
            get { return _name; }
        }

        /*public override ICollection<Pattern> Flat(params Type[] types)
        {
            if (types == null) throw new ArgumentNullException("types");
            if (types.Length == 0 || types.Contains(this.GetType()))
            {
                return new Pattern[] { this };
            }
            return new Pattern[] { };
        }*/

        public virtual SingleMatchResult SingleMatch(IList<ArgPattern> patterns)
        {
            return new SingleMatchResult();
        }

        public override MatchResult Match(IList<ArgPattern> left, IEnumerable<ArgPattern> collected = null)
        {
            var coll = collected ?? new List<ArgPattern>();
            var sresult = SingleMatch(left);
            var match = sresult.Match;
            if (match == null)
            {
                return new MatchResult(false, left, coll);
            }
            var left_ = new List<ArgPattern>();
            left_.AddRange(left.Take(sresult.Position));
            left_.AddRange(left.Skip(sresult.Position + 1));
            var sameName = coll.Where(a => a.Name == Name).ToList();
            if (Value != null && (Value.IsList || Value.IsOfTypeInt))
            {
                var increment = new ValueObject(1);
                if (!Value.IsOfTypeInt)
                {
                    increment = match.Value.IsString ? new ValueObject(new[] { match.Value }) : match.Value;
                }
                if (sameName.Count == 0)
                {
                    match.Value = increment;
                    var res = new List<ArgPattern>(coll) { match };
                    return new MatchResult(true, left_, res);
                }
                sameName[0].Value.Add(increment);
                return new MatchResult(true, left_, coll);
            }
            var resColl = new List<ArgPattern>();
            resColl.AddRange(coll);
            resColl.Add(match);
            return new MatchResult(true, left_, resColl);
        }

        public override string ToString()
        {
            return string.Format("{0}({1}, {2})", GetType().Name, Name, Value);
        }
    }

    public class ArgRequired : ArgBranchPattern
    {
        public ArgRequired(params ArgPattern[] patterns)
            : base(patterns)
        {
        }

        public override MatchResult Match(IList<ArgPattern> left, IEnumerable<ArgPattern> collected = null)
        {
            var coll = collected ?? new List<ArgPattern>();
            var l = left;
            var c = coll;
            foreach (var pattern in Children)
            {
                var res = pattern.Match(l, c);
                l = res.Left;
                c = res.Collected;
                if (!res.Matched)
                    return new MatchResult(false, left, coll);
            }
            return new MatchResult(true, l, c);
        }
    }

    public class ArgEither : ArgBranchPattern
    {
        public ArgEither(params ArgPattern[] patterns) : base(patterns)
        {
        }

        public override MatchResult Match(IList<ArgPattern> left, IEnumerable<ArgPattern> collected = null)
        {
            var coll = collected ?? new List<ArgPattern>();
            var outcomes =
                Children.Select(pattern => pattern.Match(left, coll))
                        .Where(outcome => outcome.Matched)
                        .ToList();
            if (outcomes.Count != 0)
            {
                var minCount = outcomes.Min(x => x.Left.Count);
                return outcomes.First(x => x.Left.Count == minCount);
            }
            return new MatchResult(false, left, coll);
        }
    }

    public class ArgOneOrMore : ArgBranchPattern
    {
        public ArgOneOrMore(params ArgPattern[] patterns)
            : base(patterns)
        {
        }

        public override MatchResult Match(IList<ArgPattern> left, IEnumerable<ArgPattern> collected = null)
        {
            //Debug.Assert(Children.Count == 1);
            var coll = collected ?? new List<ArgPattern>();
            var l = left;
            var c = coll;
            IList<ArgPattern> l_ = null;
            var matched = true;
            var times = 0;
            while (matched)
            {
                // could it be that something didn't match but changed l or c?
                var res = Children[0].Match(l, c);
                matched = res.Matched;
                l = res.Left;
                c = res.Collected;
                times += matched ? 1 : 0;
                if (l_ != null && l_.Equals(l))
                    break;
                l_ = l;
            }
            if (times >= 1)
            {
                return new MatchResult(true, l, c);
            }
            return new MatchResult(false, left, coll);
        }
    }

    public class ArgOptional : ArgBranchPattern
    {
        public ArgOptional(params ArgPattern[] patterns) : base(patterns)
        {

        }

        public override MatchResult Match(IList<ArgPattern> left, IEnumerable<ArgPattern> collected = null)
        {
            var c = collected ?? new List<ArgPattern>();
            var l = left;
            foreach (var pattern in Children)
            {
                var res = pattern.Match(l, c);
                l = res.Left;
                c = res.Collected;
            }
            return new MatchResult(true, l, c);
        }
    }

    public class ArgArgument : ArgLeafPattern
    {
        public ArgArgument(string name, ValueObject value = null) : base(name, value)
        {
        }

        public ArgArgument(string name, string value)
            : base(name, new ValueObject(value))
        {
        }

        public ArgArgument(string name, int value)
            : base(name, new ValueObject(value))
        {
        }

        public override SingleMatchResult SingleMatch(IList<ArgPattern> left)
        {
            for (var i = 0; i < left.Count; i++)
            {
                if (left[i] is ArgArgument)
                    return new SingleMatchResult(i, new ArgArgument(Name, left[i].Value));
            }
            return new SingleMatchResult();
        }
    }

    public class ArgOption : ArgLeafPattern
    {
        public string ShortName { get; private set; }
        public string LongName { get; private set; }
        public int ArgCount { get; private set; }

        public ArgOption(string shortName = null, int argCount = 0, ValueObject value = null)
            : base()
        {
            ShortName = shortName;
            ArgCount = argCount;
            var v = value ?? new ValueObject(false);
            Value = (v.IsFalse && argCount > 0) ? null : v;
        }

        public ArgOption(string shortName, int argCount, string value)
            : this(shortName, argCount, new ValueObject(value))
        {
        }

        public override string Name
        {
            get { return ShortName; }
        }

        public override SingleMatchResult SingleMatch(IList<ArgPattern> left)
        {
            for (var i = 0; i < left.Count; i++)
            {
                var pattern = left[i];
                if (pattern is ArgArgument)
                {
                    if (pattern.Value.ToString() == Name)
                        return new SingleMatchResult(i, new ArgCommand(Name, new ValueObject(true)));
                    else
                    {
                        Match match = Regex.Match(pattern.Value.ToString(), $"^({Name})=(.*)");
                        if (match.Success)
                        {
                            return new SingleMatchResult(i, new ArgOption(match.Groups[1].ToString(), 1, new ValueObject(match.Groups[2].ToString())));
                        }
                    }
                }
            }
            return new SingleMatchResult();
        }

        public override string ToString()
        {
            return string.Format("Option({0},{1},{2})", ShortName, ArgCount, Value);
        }
    }

    public class ArgCommand : ArgArgument
    {
        public ArgCommand(string name, ValueObject value = null) : base(name, value ?? new ValueObject(false))
        {
        }

        public override SingleMatchResult SingleMatch(IList<ArgPattern> left)
        {
            for (var i = 0; i < left.Count; i++)
            {
                var pattern = left[i];
                if (pattern is ArgArgument)
                {
                    if (pattern.Value.ToString() == Name)
                        return new SingleMatchResult(i, new ArgCommand(Name, new ValueObject(true)));
                    break;
                }
            }
            return new SingleMatchResult();
        }

        /*public override Node ToNode() { return new CommandNode(this.Name); }

        public override string GenerateCode()
        {
            var s = Name.ToLowerInvariant();
            s = "Cmd" + GenerateCodeHelper.ConvertDashesToCamelCase(s);
            return string.Format("public bool {0} {{ get {{ return _args[\"{1}\"].IsTrue; }} }}", s, Name);
        }*/

    }

    public class CommandLineArgs
    {
        public string Pattern
        {
            get { return m_pattern; }
        }

        public string Help
        {
            get { return m_help; }
        }

        protected string m_pattern;
        protected string m_help;

        private ArgRequired m_requiredPattern;

        protected CommandLineArgs()
        { }

        public CommandLineArgs(string pattern, string help)
        {
            m_pattern = pattern;
            m_help = help;

            Parse(m_pattern);
        }

        public ListDictionary Match(string text)
        {
            string[] args = Regex
                .Matches(text, "(?<match>[^\\s\"]+)|\"(?<match>[^\"]*)\"")
                  .Cast<Match>()
                .Select(m => m.Groups["match"].Value)
                .ToArray();
            return Match(args);
        }

        public ListDictionary Match(string[] argV)
        {
            var arguments = ParseArgv(argV);
            var res = m_requiredPattern.Match(arguments);
            if (res.Matched && res.LeftIsEmpty)
            {
                var dict = new ListDictionary();
                /*foreach (var p in pattern.Flat())
                {
                    dict[p.Name] = p.Value;
                }*/
                foreach (var p in res.Collected)
                {
                    if (!dict.ContainsKey(p.Name))
                    {
                        dict[p.Name] = p.Value;
                    }
                    else
                    {
                        ((ValueObject)dict[p.Name]).Append(p.Value);
                    }
                }
                return dict;
            }

            return null;
        }

        public bool IsMatch(string text)
        {
            string[] args = Regex
                .Matches(text, "(?<match>[^\\s\"]+)|\"(?<match>[^\"]*)\"")
                  .Cast<Match>()
                .Select(m => m.Groups["match"].Value)
                .ToArray();
            //string[] args = text.Split(new char[] { ' ' });
            return IsMatch(args);
        }

        public bool IsMatch(string[] args)
        {
            var arguments = ParseArgv(args);
            var res = m_requiredPattern.Match(arguments);
            if (res.Matched && res.LeftIsEmpty)
            {
                return true;
            }

            return false;

        }

        protected virtual bool Parse(string pattern)
        {
            ArgTokens tokenParser = ArgTokens.FromPattern(pattern);
            IEnumerable<ArgPattern> patterns = ParseExpression(tokenParser);
            m_requiredPattern = new ArgRequired(patterns.ToArray());
            return true;
        }

        private IEnumerable<ArgPattern> ParseExpression(ArgTokens tokenParser)
        {
            var seq = ParseSequence(tokenParser);
            if (tokenParser.Current() != "|")
                return seq;
            var result = new List<ArgPattern>();
            if (seq.Count() > 1)
            {
                result.Add(new ArgRequired(seq.ToArray()));
            }
            else
            {
                result.AddRange(seq);
            }
            while (tokenParser.Current() == "|")
            {
                tokenParser.Move();
                seq = ParseSequence(tokenParser);
                if (seq.Count() > 1)
                {
                    result.Add(new ArgRequired(seq.ToArray()));
                }
                else
                {
                    result.AddRange(seq);
                }
            }
            result = result.Distinct().ToList();
            if (result.Count > 1)
                return new[] { new ArgEither(result.ToArray()) };
            return result;
        }

        private IEnumerable<ArgPattern> ParseSequence(ArgTokens tokenParser)
        {
            var result = new List<ArgPattern>();
            while (!new[] { null, "]", ")", "|" }.Contains(tokenParser.Current()))
            {
                var atom = ParseAtom(tokenParser);
                if (tokenParser.Current() == "...")
                {
                    result.Add(new ArgOneOrMore(atom.ToArray()));
                    tokenParser.Move();
                    return result;
                }
                result.AddRange(atom);
            }
            return result;
        }

        private IEnumerable<ArgPattern> ParseAtom(ArgTokens tokenParser)
        {
            // atom ::= '(' expr ')' | '[' expr ']' | 'options'
            //  | long | shorts | argument | command ;            

            var token = tokenParser.Current();
            var result = new List<ArgPattern>();
            switch (token)
            {
                case "[":
                case "(":
                    {
                        tokenParser.Move();
                        string matching;
                        if (token == "(")
                        {
                            matching = ")";
                            result.Add(new ArgRequired(ParseExpression(tokenParser).ToArray()));
                        }
                        else
                        {
                            matching = "]";
                            result.Add(new ArgOptional(ParseExpression(tokenParser).ToArray()));
                        }
                        if (tokenParser.Move() != matching)
                            throw new Exception("unmatched '" + token + "'");
                    }
                    break;
                default:
                    if ((token.StartsWith("<") && token.EndsWith(">")) || token.All(c => Char.IsUpper(c)))
                    {
                        result.Add(new ArgArgument(tokenParser.Move()));
                    }
                    else if (token.StartsWith("-"))
                    {
                        result.Add(ParseOption(tokenParser));
                        tokenParser.Move();
                    }
                    else
                    {
                        result.Add(new ArgCommand(tokenParser.Move()));
                    }
                    break;
            }
            return result;
        }

        public ArgOption ParseOption(ArgTokens tokenParser)
        {
            var token = tokenParser.Current();
            Match match = Regex.Match(token, @"(-.*)=\<(.*)\>");
            if (match.Success)
            {
                return new ArgOption(match.Groups[1].ToString(), 1);
            }
            else
            {
                return new ArgOption(token);
            }

        }

        private static IList<ArgPattern> ParseArgv(string[] arguments)
        {
            //    If options_first:
            //        argv ::= [ long | shorts ]* [ argument ]* [ '--' [ argument ]* ] ;
            //    else:
            //        argv ::= [ long | shorts | argument ]* [ '--' [ argument ]* ] ;

            var parsed = new List<ArgPattern>();
            foreach (string arg in arguments)
            {
                parsed.Add(new ArgArgument(null, new ValueObject(arg)));
            }
            return parsed;
        }
    }

    public class DiscordCommandLineArgs : CommandLineArgs
    {
        public static char CommandKey
        {
            get;
            set;
        } = '!';

        public string Command
        {
            get { return m_command; }
        }

        private string m_command;

        public DiscordCommandLineArgs(string command, string pattern, string help)
        {
            m_command = command;
            m_pattern = pattern;
            m_help = help;

            Parse(m_pattern);
        }

        protected override bool Parse(string pattern)
        {
            string fullPattern = CommandKey + m_command + " " + m_pattern;
            return base.Parse(fullPattern);
        }
    }
}
