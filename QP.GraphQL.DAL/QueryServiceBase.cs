using QP.GraphQL.Interfaces.Articles.Filtering;
using QP.GraphQL.Interfaces.DAL;
using System;
using System.Data;
using System.Data.Common;

namespace QP.GraphQL.DAL
{
    public abstract class QueryServiceBase
    {
        public IQueryContext GetQueryContext(QpFieldFilterClause clause)
        {
            var leftPart = clause.FilterDefinition.QpFieldName;
            var rightPart = clause.Value;

            string op = clause.FilterDefinition.Operator switch
            {
                FilterOperator.Equal => "=",
                FilterOperator.NotEqual => "!=",
                FilterOperator.GreaterThan => ">",
                FilterOperator.GreaterOrEqual => ">=",
                FilterOperator.LessThan => "<",
                FilterOperator.LessOrEqual => "<=",
                FilterOperator.Like => "like",
                FilterOperator.NotLike => "not like",
                _ => throw new NotImplementedException($"Unsupported operator {clause.FilterDefinition.Operator}")
            };

            if (clause.FilterDefinition.Operator == FilterOperator.Like || clause.FilterDefinition.Operator == FilterOperator.NotLike)
            {
                //хак для регистронезависимости
                leftPart = $"lower({leftPart})";
                rightPart = (rightPart as string).ToLowerInvariant();
            }

            var param = GetParameter(clause.FilterDefinition.QpFieldName, clause.FilterDefinition.QpFieldType, rightPart);
            return new QueryContext($"({leftPart} {op} {param.ParameterName})", param);
        }


        public abstract DbParameter GetParameter(string name, SqlDbType type, object value);

        protected DbParameter GetParameter(string name, string type, object value) => GetParameter(name, GetSqlType(type), value);

        protected SqlDbType GetSqlType(string paramType)
        {
            return paramType switch
            {
                "String" => SqlDbType.NVarChar,
                "Numeric" => SqlDbType.Decimal,
                "Boolean" => SqlDbType.Bit,
                "Date" => SqlDbType.DateTime,
                "Time" => SqlDbType.Time,
                "DateTime" => SqlDbType.DateTime,
                "File" => SqlDbType.NVarChar,
                "Image" => SqlDbType.NVarChar,
                "Textbox" => SqlDbType.NText,
                "VisualEdit" => SqlDbType.NText,
                "Relation" => SqlDbType.Decimal,
                "Dynamic Image" => SqlDbType.NVarChar,
                "Relation Many-to-One" => SqlDbType.Decimal,
                _ => throw new ArgumentException("Can't be mapped to SqlDbType", nameof(paramType))
            };
        }

        protected string GetParamName(string name)
        {
            return $"@{name}_{Guid.NewGuid():N}";
        }
    }
}
