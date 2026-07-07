#region Copyright (c) 2023-2026 SnowBank SAS, (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of SnowBank nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL SNOWBANK SAS BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

namespace SnowBank.Runtime
{
	using System.Linq.Expressions;
	using System.Reflection;

	public static class ExpressionExtensions
	{

		private static readonly PropertyInfo DebugViewProperty = typeof(Expression).GetProperty("DebugView", BindingFlags.Instance | BindingFlags.NonPublic)!;

		/// <summary>Returns the value of the private <b>DebugView</b> property of an <see cref="Expression"/></summary>
		public static string GetDebugView(this Expression? expr)
		{
			return expr == null ? "<null>" : (string) (DebugViewProperty.GetValue(expr) ?? "<null>");
		}

		/// <summary>Casts an expression of type 'object' to an instance of type <paramref name="targetType"/>: '(TYPE) obj'</summary>
		/// <remarks>
		/// Generates the appropriate expression depending on whether <paramref name="targetType"/> is a ValueType or not.
		/// WARNING: in the case of a boxed valuetype/struct, we will return a COPY of the value. If the caller wants to modify it, it will therefore not modify the original!
		/// </remarks>
		public static Expression CastFromObject(this Expression expr, Type targetType)
		{
			Contract.NotNull(expr);
			Contract.NotNull(targetType);
			// IMPORTANT: if we cast a struct, we must in that case go through Expression.Unbox to "unbox" the valuetype, and not call Convert (which makes a copy)
			// Note: if the expression is already the correct type, we do nothing
			return expr.Type == targetType ? expr : targetType.IsClass ? Expression.TypeAs(expr, targetType) : targetType.IsValueType ? Expression.Unbox(expr, targetType) : Expression.Convert(expr, targetType);
		}

		/// <summary>Boxes an expression to object: '(object) expr'</summary>
		/// <remarks>If the expression's type is a ValueType, then it will be boxed automatically.</remarks>
		public static Expression BoxToObject(this Expression expr)
		{
			Contract.NotNull(expr);
			// note: if the expression is already an object, we do nothing
			return typeof(object) == expr.Type ? expr : expr.Type.IsClass ? expr/*Expression.TypeAs(expr, typeof(object))*/ : Expression.Convert(expr, typeof(object));
		}

		/// <summary>Returns an expression '(expr == null)'</summary>
		public static Expression IsNull(this Expression expr)
		{
			Contract.NotNull(expr);
			//REVIEW: what to do if struct? return a constant "false" ?
			return Expression.Equal(expr, Expression.Default(typeof(object)));
		}

		/// <summary>Returns an expression '(expr != null)'</summary>
		public static Expression IsNotNull(this Expression expr)
		{
			Contract.NotNull(expr);
			//REVIEW: what to do if struct? return a constant "true" ?
			return Expression.NotEqual(expr, Expression.Default(typeof(object)));
		}

		/// <summary>Returns an expression 'expr.MEMBER' adapted according to the member's type (field, property, ...)</summary>
		public static Expression PropertyOrField(this Expression expr, MemberInfo info)
		{
			Contract.NotNull(expr);
			Contract.NotNull(info);
			switch (info)
			{
				case PropertyInfo prop:
				{
					return Expression.Property(expr, prop);
				}
				case FieldInfo field:
				{
					return Expression.Field(expr, field);
				}
				default:
				{
					throw new InvalidOperationException($"Cannot create Getter for member of type {info.MemberType}.");
				}
			}
		}

	}
}
