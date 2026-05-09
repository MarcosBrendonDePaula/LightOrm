using System;
using LightOrm.Core.Validation;

namespace LightOrm.Core.Tests
{
    public class ValidationAttributeUnitTests
    {
        [Fact]
        public void Required_rejects_null_and_empty_but_allows_non_empty()
        {
            var attr = new RequiredAttribute();

            Assert.NotNull(attr.Validate(null));
            Assert.NotNull(attr.Validate(string.Empty));
            Assert.Null(attr.Validate("ok"));
        }

        [Fact]
        public void Length_attributes_only_apply_to_strings()
        {
            var min = new MinLengthAttribute(3);
            var max = new MaxLengthAttribute(5);

            Assert.Null(min.Validate(123));
            Assert.Null(max.Validate(123));
            Assert.NotNull(min.Validate("ab"));
            Assert.NotNull(max.Validate("abcdef"));
        }

        [Fact]
        public void Regex_attribute_ignores_null_and_rejects_non_matching_string()
        {
            var attr = new RegExAttribute("^[A-Z]+$");

            Assert.Null(attr.Validate(null));
            Assert.Null(attr.Validate("ABC"));
            Assert.NotNull(attr.Validate("Abc"));
        }

        [Fact]
        public void Range_attribute_accepts_null_and_non_numeric_values_but_rejects_out_of_range_numbers()
        {
            var attr = new RangeAttribute(0, 10);

            Assert.Null(attr.Validate(null));
            Assert.Null(attr.Validate(DateTime.UtcNow));
            Assert.Null(attr.Validate("not-a-number"));
            Assert.Null(attr.Validate(10));
            Assert.NotNull(attr.Validate(11));
            Assert.NotNull(attr.Validate(-1));
        }
    }
}
