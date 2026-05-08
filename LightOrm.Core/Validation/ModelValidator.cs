using System;
using System.Collections.Generic;
using System.Reflection;
using LightOrm.Core.Utilities;

namespace LightOrm.Core.Validation
{
    public static class ModelValidator
    {
        public static void Validate(object entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            var errors = new List<ValidationError>();
            foreach (var prop in TypeMetadataCache.GetProperties(entity.GetType()))
            {
                var validators = prop.GetCustomAttributes<ValidationAttribute>(inherit: true);
                object value = null;
                bool valueRead = false;
                foreach (var v in validators)
                {
                    if (!valueRead) { value = prop.GetValue(entity); valueRead = true; }
                    var msg = v.Validate(value);
                    if (msg != null)
                        errors.Add(new ValidationError(prop.Name, msg));
                }
            }
            if (errors.Count > 0) throw new ValidationException(errors);
        }
    }
}
