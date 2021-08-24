using GraphQL.Language.AST;
using GraphQL.Types;
using System;
using System.Globalization;

namespace QP.GraphQL.App.Types
{
    public class TimeGraphType : ScalarGraphType
    {
        private const string TimeFormat = "c";

        public TimeGraphType()
        {
            Description =
                "The `Time` scalar type represents a time. `Time` expects timespan " +
                "to be formatted in accordance with the [ISO-8601](https://en.wikipedia.org/wiki/ISO_8601) standard.";
        }

        public override object ParseLiteral(IValue value) => value switch
        {
            StringValue stringValue => ParseTime(stringValue.Value),
            NullValue _ => null,
            _ => ThrowLiteralConversionError(value)
        };

        public override object ParseValue(object value) => value switch
        {
            TimeSpan _ => value,
            string s => ParseTime(s),
            null => null,
            _ => ThrowValueConversionError(value)
        };

        private static TimeSpan ParseTime(string stringValue)
        {
            if (TimeSpan.TryParseExact(stringValue, TimeFormat, DateTimeFormatInfo.InvariantInfo, out var time))
            {
                return time;
            }

            throw new FormatException($"Could not parse time. Expected ISO-8601 format. Value: {stringValue}");
        }

        public override object Serialize(object value) => value switch
        {
            TimeSpan d => d.ToString(TimeFormat, DateTimeFormatInfo.InvariantInfo),
            null => null,
            _ => ThrowSerializationError(value)
        };
    }
}
