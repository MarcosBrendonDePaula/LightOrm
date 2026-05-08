using System;

namespace LightOrm.Core.Validation
{
    /// <summary>
    /// Base abstrata: cada validador implementa Validate(value) e devolve
    /// null se ok, string com mensagem de erro caso contrário.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public abstract class ValidationAttribute : Attribute
    {
        public abstract string Validate(object value);
    }

    public class RequiredAttribute : ValidationAttribute
    {
        public override string Validate(object value)
        {
            if (value == null) return "obrigatório";
            if (value is string s && string.IsNullOrEmpty(s)) return "obrigatório";
            return null;
        }
    }

    public class MaxLengthAttribute : ValidationAttribute
    {
        public int Length { get; }
        public MaxLengthAttribute(int length) { Length = length; }

        public override string Validate(object value)
        {
            if (value is string s && s.Length > Length)
                return $"máximo {Length} caracteres (recebido {s.Length})";
            return null;
        }
    }

    public class MinLengthAttribute : ValidationAttribute
    {
        public int Length { get; }
        public MinLengthAttribute(int length) { Length = length; }

        public override string Validate(object value)
        {
            if (value is string s && s.Length < Length)
                return $"mínimo {Length} caracteres (recebido {s.Length})";
            return null;
        }
    }

    public class RegExAttribute : ValidationAttribute
    {
        private readonly System.Text.RegularExpressions.Regex _regex;
        public string Pattern { get; }

        public RegExAttribute(string pattern)
        {
            Pattern = pattern;
            _regex = new System.Text.RegularExpressions.Regex(pattern);
        }

        public override string Validate(object value)
        {
            if (value is string s && !_regex.IsMatch(s))
                return $"não casa com padrão {Pattern}";
            return null;
        }
    }

    public class RangeAttribute : ValidationAttribute
    {
        public double Min { get; }
        public double Max { get; }

        public RangeAttribute(double min, double max)
        {
            Min = min;
            Max = max;
        }

        public override string Validate(object value)
        {
            if (value == null) return null;
            if (value is IConvertible)
            {
                try
                {
                    var d = Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
                    if (d < Min || d > Max) return $"fora do intervalo [{Min}, {Max}]";
                }
                catch { /* tipos não-numéricos passam */ }
            }
            return null;
        }
    }
}
