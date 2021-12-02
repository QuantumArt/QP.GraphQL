using QP.GraphQL.Interfaces.Articles.Filtering;
using QP.GraphQL.Interfaces.DAL;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;

namespace QP.GraphQL.DAL
{
    public abstract class QueryServiceBase
    {
        public IQueryContext GetQueryContext(QpFieldRelationFilterClause clause)
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
                FilterOperator.IsNull => (bool)clause.Value ? "is null" : "is not null",
                FilterOperator.In => "in",
                FilterOperator.NotIn => "not in",
                FilterOperator.Contains => "exists",
                FilterOperator.NotContains => "not exists",
                _ => throw new NotImplementedException($"Unsupported operator {clause.FilterDefinition.Operator}")
            };

            if (clause.FilterDefinition.Operator == FilterOperator.Like || clause.FilterDefinition.Operator == FilterOperator.NotLike)
            {
                //хак для регистронезависимости
                leftPart = $"lower({leftPart})";
                rightPart = (rightPart as string).ToLowerInvariant();
            }

            if (clause.FilterDefinition.Operator == FilterOperator.IsNull)
            {
                return new QueryContext($"({leftPart} {op})");
            }
            else if (clause.FilterDefinition.Operator == FilterOperator.In || clause.FilterDefinition.Operator == FilterOperator.NotIn)
            {
                var ids = GetIds(rightPart);
                var param = GetIdParam(GetParamName(clause.FilterDefinition.QpFieldName), ids);
                return new QueryContext($"({leftPart} {op} (select id from {GetIdTable(param.ParameterName)}))", param);
            }
            else if (clause.FilterDefinition.Operator == FilterOperator.Contains || clause.FilterDefinition.Operator == FilterOperator.NotContains)
            {
                var ids = GetIds(rightPart);
                var param = GetIdParam(GetParamName(clause.FilterDefinition.QpFieldName), ids);

                var query = @$"({op} (
                    select null
                    from {clause.BackwardTable} backward
                    where
                        {clause.TableAlias}.content_item_id = backward.{clause.BackwardField} and
                        backward.content_item_id in (select id from {GetIdTable(param.ParameterName)}
                )))";

                return new QueryContext(query, param);
            }
            else
            {
                var param = GetParameter(clause.FilterDefinition.QpFieldName, clause.FilterDefinition.QpFieldType, rightPart);
                return new QueryContext($"({leftPart} {op} {param.ParameterName})", param);
            }
        }

        private int[] GetIds(object values)
        {
            if (values == null)
            {
                return new int[0];
            }
            else
            {
                return ((object[])values).Select(v => Convert.ToInt32(v)).ToArray();
            }
        }

        public abstract DbParameter GetIdParam(string name, IEnumerable<int> ids);

        public abstract string GetIdTable(string name, string alias = "i");

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
