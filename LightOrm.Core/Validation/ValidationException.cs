using System;
using System.Collections.Generic;
using System.Linq;

namespace LightOrm.Core.Validation
{
    /// <summary>
    /// Lançada quando um modelo falha validação declarativa antes do SaveAsync.
    /// Errors lista cada falha com nome da propriedade e mensagem.
    /// </summary>
    public class ValidationException : Exception
    {
        public IReadOnlyList<ValidationError> Errors { get; }

        public ValidationException(IReadOnlyList<ValidationError> errors)
            : base(BuildMessage(errors))
        {
            Errors = errors;
        }

        private static string BuildMessage(IReadOnlyList<ValidationError> errors)
        {
            if (errors == null || errors.Count == 0) return "Validação falhou.";
            return "Validação falhou: " + string.Join("; ",
                errors.Select(e => $"{e.PropertyName}: {e.Message}"));
        }
    }

    public class ValidationError
    {
        public string PropertyName { get; }
        public string Message { get; }

        public ValidationError(string propertyName, string message)
        {
            PropertyName = propertyName;
            Message = message;
        }
    }
}
