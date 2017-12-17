﻿#region License
// The PostgreSQL License
//
// Copyright (C) 2016 The Npgsql Development Team
//
// Permission to use, copy, modify, and distribute this software and its
// documentation for any purpose, without fee, and without a written
// agreement is hereby granted, provided that the above copyright notice
// and this paragraph and the following two paragraphs appear in all copies.
//
// IN NO EVENT SHALL THE NPGSQL DEVELOPMENT TEAM BE LIABLE TO ANY PARTY
// FOR DIRECT, INDIRECT, SPECIAL, INCIDENTAL, OR CONSEQUENTIAL DAMAGES,
// INCLUDING LOST PROFITS, ARISING OUT OF THE USE OF THIS SOFTWARE AND ITS
// DOCUMENTATION, EVEN IF THE NPGSQL DEVELOPMENT TEAM HAS BEEN ADVISED OF
// THE POSSIBILITY OF SUCH DAMAGE.
//
// THE NPGSQL DEVELOPMENT TEAM SPECIFICALLY DISCLAIMS ANY WARRANTIES,
// INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY
// AND FITNESS FOR A PARTICULAR PURPOSE. THE SOFTWARE PROVIDED HEREUNDER IS
// ON AN "AS IS" BASIS, AND THE NPGSQL DEVELOPMENT TEAM HAS NO OBLIGATIONS
// TO PROVIDE MAINTENANCE, SUPPORT, UPDATES, ENHANCEMENTS, OR MODIFICATIONS.
#endregion

using System;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Query.Expressions;

namespace Microsoft.EntityFrameworkCore.Query.ExpressionTranslators.Internal
{
    public class NpgsqlStringTrimEndTranslator : IMethodCallTranslator
    {
        static readonly MethodInfo TrimEndWithChars
            = typeof(string).GetRuntimeMethod(nameof(string.TrimEnd), new[] { typeof(char[]) });

        // The following exists as an optimization in netcoreapp20
        static readonly MethodInfo TrimEndNoParam
            = typeof(string).GetRuntimeMethod(nameof(string.TrimEnd), new Type[0]);

        // The following exists as an optimization in netcoreapp20
        static readonly MethodInfo TrimEndSingleChar
            = typeof(string).GetRuntimeMethod(nameof(string.TrimEnd), new[] { typeof(char) });

        public virtual Expression Translate(MethodCallExpression methodCallExpression)
        {
            if (!methodCallExpression.Method.Equals(TrimEndWithChars) &&
                !methodCallExpression.Method.Equals(TrimEndNoParam) &&
                !methodCallExpression.Method.Equals(TrimEndSingleChar))
            {
                return null;
            }

            char[] trimChars;

            if (methodCallExpression.Method.Equals(TrimEndSingleChar))
            {
                var constantTrimChars = methodCallExpression.Arguments[0] as ConstantExpression;
                if (constantTrimChars == null)
                    return null;  // Don't translate if trim chars isn't a constant

                var trimChar = (char)constantTrimChars.Value;
                return new SqlFunctionExpression(
                    "RTRIM",
                    typeof(string),
                    new[]
                    {
                        methodCallExpression.Object,
                        Expression.Constant(new string(trimChar, 1))
                    });
            }

            if (methodCallExpression.Method.Equals(TrimEndNoParam))
            {
                trimChars = null;
            }
            else if (methodCallExpression.Method.Equals(TrimEndWithChars))
            {
                var constantTrimChars = methodCallExpression.Arguments[0] as ConstantExpression;
                if (constantTrimChars == null)
                {
                    return null; // Don't translate if trim chars isn't a constant
                }

                trimChars = (char[])constantTrimChars.Value;
            }
            else
                throw new Exception($"{nameof(NpgsqlStringTrimEndTranslator)} does not support {methodCallExpression}");

            if (trimChars == null || trimChars.Length == 0)
            {
                // Trim whitespace
                return new SqlFunctionExpression(
                    "REGEXP_REPLACE",
                    typeof(string),
                    new[]
                    {
                    methodCallExpression.Object,
                    Expression.Constant(@"\s*$"),
                    Expression.Constant(string.Empty)
                });
            }

            return new SqlFunctionExpression(
                "RTRIM",
                typeof(string),
                new[]
                {
                    methodCallExpression.Object,
                    Expression.Constant(new string(trimChars))
                });
        }
    }
}
