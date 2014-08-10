using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
namespace cmdValidator
{
	public class Validator
	{
        private const string MESSAGE_INVALID_ARGUMENT_SCHEME = "The argument scheme is invalid.";
        private const string MESSAGE_CMD_ALREADY_EXISTS = "Cmd already exists. It's only allowed to use every cmd one time.";

		private string _identifierPattern;
		private string _stringSplitPattern;
		private string _argumentPattern;
		private string[] _optionPrefixes;
		private bool _ignoreUnknownParameters;
		private List<ArgumentSet> _argumentSets;
		public bool IgnoreUnknownParameters
		{
			get
			{
				return this._ignoreUnknownParameters;
			}
			set
			{
				this._ignoreUnknownParameters = value;
			}
		}
		public Validator(bool ignoreUnknownParameters) : this(ignoreUnknownParameters, null)
		{
		}
		private Validator(bool ignoreUnknownParameters, string[] separators)
		{
			this._identifierPattern = "(?:(?:(\\w+)(\\[\\w+\\])?(\\w*))|(?:(\\[\\w+\\])(\\w+)))";
			this._stringSplitPattern = "(\"([^\"]+)\"|[^(\\s|,)]+),?";
			this._argumentPattern = "\\A(?:(?:(?:\\w+)(?:\\[\\w+\\])?(?:\\w*))|(?:(?:\\[\\w+\\])(?:\\w+)))(?:\\|(?:(?:(?:\\w+)(?:\\[\\w+\\])?(?:\\w*))|(?:(?:\\[\\w+\\])(?:\\w+))))*(?:(?::\\([\"\\w ]+(?:\\|[\"\\w ]+)*\\))|(?::[\"\\w ]+(?:\\|[\"\\w ]+)*))?\\Z";
			if (separators == null)
			{
				this._optionPrefixes = new string[]
				{
					"-",
					"--",
					"/"
				};
			}
			else
			{
				this._optionPrefixes = separators;
			}
			this._optionPrefixes = this.SortByLengthDescending(this._optionPrefixes).Cast<string>().ToArray<string>();
			this._ignoreUnknownParameters = ignoreUnknownParameters;
			this._argumentSets = new List<ArgumentSet>();
		}
		private IEnumerable<string> SortByLengthDescending(IEnumerable<string> e)
		{
			return 
				from s in e
				orderby s.Length descending
				select s;
		}
		public void AddArgumentSet(string argumentSchemesString, GetArguments getArgs)
		{
			IEnumerable<ArgumentScheme> argumentSchemes = this.GetArgumentSchemes(argumentSchemesString);

            ArgumentSet newArgumentSet = new ArgumentSet(argumentSchemes, getArgs);

            //checks if a cmd already exists,
            //if not the argSet is legal and will be added to the argumentSets,
            //otherwise a exception will be thrown
            foreach (var argumentSet in this._argumentSets)
                foreach (var newIdentifier in newArgumentSet.GetCmd().Identifiers)
                    foreach (var identifier in argumentSet.GetCmd().Identifiers)
                        if(identifier.Contains(newIdentifier))
                            throw new Exception(string.Format("{0}\nduplicative cmd: {1}", MESSAGE_CMD_ALREADY_EXISTS, newIdentifier));

			this._argumentSets.Add(new ArgumentSet(argumentSchemes, getArgs));
		}
		public bool CheckArgs(string args)
		{
			return this.CheckArgs(this.SplitArgs(args).Cast<string>().ToList<string>());
		}
		public bool CheckArgs(List<string> args)
		{
			bool result = false;
			for (int i = 0; i < this._argumentSets.Count; i++)
			{
				List<string> unknownParameters = new List<string>();
				ArgumentSet parsedArgumentSet = this.GetParsedArgumentSet(this._argumentSets[i], args, this._ignoreUnknownParameters, ref unknownParameters);
				if (parsedArgumentSet != null)
				{
					this._argumentSets[i] = parsedArgumentSet;
					if (this._ignoreUnknownParameters || (!this._ignoreUnknownParameters && unknownParameters.Count == 0))
					{
						parsedArgumentSet.TriggerEvent(unknownParameters.ToArray());
					}
					result = true;
				}
			}
			return result;
		}
		private ArgumentSet GetParsedArgumentSet(ArgumentSet argSet, List<string> args, bool ignoreUnknownParameters, ref List<string> unknownOptions)
		{
			List<ArgumentScheme> list = new List<ArgumentScheme>();

			for (int i = 0; i < argSet.ArgSchemes.Length; i++)
			{
				ArgumentScheme parsedArgumentScheme = this.GetParsedArgumentScheme(argSet.ArgSchemes[i], ref args);
				if (parsedArgumentScheme == null)
					return null;

				list.Add(parsedArgumentScheme);
			}
			unknownOptions = new List<string>(args);
			argSet.ArgSchemes = list.ToArray();
			return argSet;
		}
        private ArgumentScheme GetParsedArgumentScheme(ArgumentScheme argScheme, ref List<string> args)
        {
            for (int i = 0; i < args.Count; i++)
            {
                int num = -1;
                string identifier = this.GetIdentifier(argScheme.Identifiers, args, out num);
                if (identifier != "")
                {
                    int num2 = num;
                    i = num + 1;
                    switch (argScheme.ValueType)
                    {
                        case ValueType.None:
                            {
                                int num3 = num2;
                                //args = this.RemoveItems(num2, num3, args);
                                args.Remove(identifier);
                                return argScheme;
                            }
                        case ValueType.Single:
                            if (i < args.Count)
                            {
                                if (this.IsOption(args[i]) == -1)
                                {
                                    if ((argScheme.AllowedValues.Count > 0 && argScheme.AllowedValues.Contains(args[i])) || argScheme.AllowedValues.Count == 0)
                                    {
                                        argScheme.ParsedValues.Add(args[i]);
                                        int num3 = i;
                                        //args = this.RemoveItems(num2, num3, args);
                                        args.Remove(identifier);
                                        return argScheme;
                                    }
                                    if (argScheme.OptionalValues)
                                    {
                                        return argScheme;
                                    }
                                    return null;
                                }
                            }
                            else
                            {
                                if (argScheme.OptionalValues)
                                {
                                    int num3 = num2;
                                    //args = this.RemoveItems(num2, num3, args);
                                    args.Remove(identifier);
                                    return argScheme;
                                }
                            }
                            return null;
                        case ValueType.List:
                            {
                                List<string> list = new List<string>();
                                int num3 = num2;
                                for (int j = num3 + 1; j < args.Count; j++)
                                {
                                    if (this.IsOption(args[j]) != -1)
                                    {
                                        break;
                                    }
                                    list.Add(args[j]);
                                    num3 = j;
                                }
                                //args = this.RemoveItems(num2, num3, args);
                                args.Remove(identifier);
                                if (list.Count > 0)
                                {
                                    argScheme.ParsedValues = list;
                                    return argScheme;
                                }
                                if (argScheme.OptionalValues)
                                {
                                    return argScheme;
                                }
                                return null;
                            }
                    }
                }
                else
                {
                    if (argScheme.IsOptional)
                    {
                        return argScheme;
                    }
                }
            }
            return null;
        }
        private string[] RemoveItems(int minIndex, int maxIndex, string[] text)
        {
            List<string> list = new List<string>();
            for (int i = 0; i < minIndex; i++)
            {
                list.Add(text[i]);
            }
            for (int j = maxIndex + 1; j < text.Length; j++)
            {
                list.Add(text[j]);
            }
            return list.ToArray<string>();
        }
        private string GetIdentifier(IEnumerable<string> identifiers, List<string> args, out int indexIdentifier)
        {
            string[] array = identifiers.Cast<string>().ToArray<string>();
            string result;
            for (int i = 0; i < args.Count; i++)
            {
                int num = this.IsOption(args[i]);
                if (num > -1)
                {
                    string text = args[i].Substring(this._optionPrefixes[num].Length);
                    if (identifiers.Contains(text))
                    {
                        indexIdentifier = i;
                        return text;
                    }
                }
                else
                {
                    if(identifiers.Contains(args[i]))
                    {
                        indexIdentifier = 0;
                        return args[i];
                    }
                }
            }
            indexIdentifier = -1;
            result = "";
            return result;
        }
        //private string GetIdentifier(IEnumerable<string> identifiers, string[] args, out int indexIdentifier)
        //{
        //    string[] array = identifiers.Cast<string>().ToArray<string>();
        //    string result;
        //    for (int i = 0; i < args.Length; i++)
        //    {
        //        int num = this.IsOption(args[i]);
        //        if (num > -1)
        //        {
        //            string text = args[i].Substring(this._separators[num].Length);
        //            if (identifiers.Contains(text))
        //            {
        //                indexIdentifier = i;
        //                result = text;
        //                return result;
        //            }
        //        }
        //    }
        //    indexIdentifier = -1;
        //    result = "";
        //    return result;
        //}
		private int IsOption(string argument)
		{
			int result;
			for (int i = 0; i < this._optionPrefixes.Length; i++)
			{
				if (argument.StartsWith(this._optionPrefixes[i]))
				{
					result = i;
					return result;
				}
			}
			result = -1;
			return result;
		}
		private string GetOptionWithoutSeparator(string option, int separatorIndex)
		{
			return option.Substring(this._optionPrefixes[separatorIndex].Length - 1);
		}
		private IEnumerable<string> SplitArgs(string args)
		{
			Regex regex = new Regex(this._stringSplitPattern);
			MatchCollection matchCollection = regex.Matches(args);
			foreach (Match match in matchCollection)
			{
				string text = match.Groups[2].ToString();
				if (text != string.Empty)
				{
					yield return text;
				}
				else
				{
					yield return match.Groups[1].ToString();
				}
			}
			yield break;
		}
		public IEnumerable<ArgumentScheme> GetArgumentSchemes(string argumentSchemes)
		{
			argumentSchemes = argumentSchemes.Trim();
			List<ArgumentScheme> list = new List<ArgumentScheme>();
			string[] array = argumentSchemes.Split(new char[]
			{
				','
			});

            //if there are no argument schemes stop parsing and throw exception
            if (array.Length < 1)
                throw new Exception(string.Format("{0}\nThe argument schemes are empty.", MESSAGE_INVALID_ARGUMENT_SCHEME));

            //first argumentScheme is always the cmd
            list.Add(GetArgumentScheme(array[0], true));

            //every other argumentScheme cannot be the cmd
            for (int i = 1; i < array.Length; i++)
                list.Add(GetArgumentScheme(array[i], false));

			return list;
		}

        private ArgumentScheme GetArgumentScheme(string argumentSchemeString, bool isCmd)
        {
            ArgumentScheme argumentScheme = this.GetArgument(argumentSchemeString, isCmd);

            if (argumentScheme == null)
            {
                throw new Exception(string.Format("{0}\nCorrupt argument scheme: {1}",
                    MESSAGE_INVALID_ARGUMENT_SCHEME, argumentSchemeString));
            }

            return argumentScheme;
        }

		private ArgumentScheme GetArgument(string argumentScheme, bool isCmd)
		{
			bool isOptional = this.CheckFirstAndLastCharOfString('(', ')', argumentScheme);
			if (isOptional)
			{
				argumentScheme = this.RemoveLastAndFirstChar(argumentScheme);
			}
			ArgumentScheme result;
			if (this.IsValid(argumentScheme))
			{
				ValueType valueType = ValueType.None;
				int num = argumentScheme.IndexOf(':');
				if (num == -1)
				{
					num = argumentScheme.IndexOf('=');
				}
				if (num > -1)
				{
					string valueScheme = argumentScheme.Substring(num + 1);
					bool optionalValues;
					string[] values2 = this.GetValues(valueScheme, out optionalValues, out valueType).Cast<string>().ToArray<string>();
					string multipleIdentifierScheme = argumentScheme.Substring(0, num);

                    result = new ArgumentScheme(this.GetIdentifiers(multipleIdentifierScheme), values2, valueType, optionalValues, isOptional, isCmd);
				}
				else
				{
					string multipleIdentifierScheme = argumentScheme;

                    result = new ArgumentScheme(this.GetIdentifiers(multipleIdentifierScheme), new List<string>(), ValueType.None, false, isOptional, isCmd);
				}
			}
			else
			{
				result = null;
			}
			return result;
		}
		private IEnumerable<string> GetValues(string valueScheme, out bool optionalValues, out ValueType valueType)
		{
			valueScheme = valueScheme.ToLower();
			if (this.CheckFirstAndLastCharOfString('(', ')', valueScheme))
			{
				optionalValues = true;
				valueScheme = this.RemoveLastAndFirstChar(valueScheme);
			}
			else
			{
				optionalValues = false;
			}
			IEnumerable<string> result;
			if (valueScheme == "\"list" || valueScheme == "\"l")
			{
				valueType = ValueType.List;
				result = new string[0];
			}
			else
			{
				if (valueScheme == "\"single" || valueScheme == "\"s")
				{
					valueType = ValueType.Single;
					result = new string[0];
				}
				else
				{
					valueType = ValueType.Single;
					result = valueScheme.Split(new char[]
					{
						'|'
					});
				}
			}
			return result;
		}
		private bool IsValid(string argument)
		{
			Regex regex = new Regex(this._argumentPattern);
			return regex.IsMatch(argument);
		}
		private IEnumerable<string> GetIdentifiers(string multipleIdentifierScheme)
		{
			List<string> list = new List<string>();
			string[] array = multipleIdentifierScheme.Split(new char[]
			{
				'|'
			});
			string[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				string singleIdentifierScheme = array2[i];
				list.AddRange(this.SplitIdentifiers(singleIdentifierScheme));
			}
			return list;
		}
		private IEnumerable<string> SplitIdentifiers(string singleIdentifierScheme)
		{
			List<string> list = new List<string>();
			Regex regex = new Regex(this._identifierPattern);
			Match match = regex.Match(singleIdentifierScheme);
			string[] array = new string[5];
			for (int i = 0; i < 5; i++)
			{
				array[i] = match.Groups[i + 1].ToString();
			}
			if (array[0] != string.Empty)
			{
				if (array[1] != string.Empty)
				{
					if (array[2] != string.Empty)
					{
						list.Add(array[0] + array[2]);
						list.Add(array[0] + this.RemoveLastAndFirstChar(array[1]) + array[2]);
					}
					else
					{
						list.Add(array[0]);
						list.Add(array[0] + this.RemoveLastAndFirstChar(array[1]));
					}
				}
				else
				{
					list.Add(array[0]);
				}
			}
			else
			{
				list.Add(this.RemoveLastAndFirstChar(array[3]) + array[4]);
				list.Add(array[4]);
			}
			return list;
		}
		private string RemoveLastAndFirstChar(string text)
		{
			string result;
			if (text.Length >= 3)
			{
				result = text.Substring(1, text.Length - 2);
			}
			else
			{
				result = text;
			}
			return result;
		}
		private bool CheckFirstAndLastCharOfString(char firstChar, char lastChar, string text)
		{
			return text.Length > 1 && text[0] == firstChar && text[text.Length - 1] == lastChar;
		}
	}
}