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

		/// <summary>Retourne la valeur de la propriété privée <b>DebugView</b> d'une <see cref="Expression"/></summary>
		public static string GetDebugView(this Expression? expr)
		{
			return expr == null ? "<null>" : (string) (DebugViewProperty.GetValue(expr) ?? "<null>");
		}

		/// <summary>Cast une expression de type 'object' vers une instance de type <paramref name="targetType"/>: '(TYPE) obj'</summary>
		/// <remarks>
		/// Génère l'expression appropriée suivant que <paramref name="targetType"/> est un ValueType ou non.
		/// ATTENTION: dan le cas où on à un valuetype/struct boxé, on retournera une COPIE de la valeur. Si l'appelant veut la modifier, il ne modifiera donc pas l'original !
		/// </remarks>
		public static Expression CastFromObject(this Expression expr, Type targetType)
		{
			Contract.NotNull(expr);
			Contract.NotNull(targetType);
			// IMPORTANT: si on cast une struct,  il faut dans ce cas passer par Expression.Unbox pour "unboxer" le valuetype, et non pas appeler Convert (qui fait une copie)
			// Note: si l'expression est déjà dans le bon type, on ne fait rien
			return expr.Type == targetType ? expr : targetType.IsClass ? Expression.TypeAs(expr, targetType) : targetType.IsValueType ? Expression.Unbox(expr, targetType) : Expression.Convert(expr, targetType);
		}

		/// <summary>Box une expression vers object: '(object) expr'</summary>
		/// <remarks>Si le type de l'expression est un ValueType, alors il sera boxé automatiquement.</remarks>
		public static Expression BoxToObject(this Expression expr)
		{
			Contract.NotNull(expr);
			// note: si l'expression est déjà un object, on ne fait rien
			return typeof(object) == expr.Type ? expr : expr.Type.IsClass ? expr/*Expression.TypeAs(expr, typeof(object))*/ : Expression.Convert(expr, typeof(object));
		}

		/// <summary>Retourne une expression '(expr == null)'</summary>
		public static Expression IsNull(this Expression expr)
		{
			Contract.NotNull(expr);
			//REVIEW: que faire si struct? retourner une constante "false" ?
			return Expression.Equal(expr, Expression.Default(typeof(object)));
		}

		/// <summary>Retourne une expression '(expr != null)'</summary>
		public static Expression IsNotNull(this Expression expr)
		{
			Contract.NotNull(expr);
			//REVIEW: que faire si struct? retourner une constante "true" ?
			return Expression.NotEqual(expr, Expression.Default(typeof(object)));
		}

		/// <summary>Retourne une expression 'expr.MEMBER' adaptée en fonction du type du membre (field, property, ...)</summary>
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
