﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using Morestachio.Attributes;
using Morestachio.Formatter;

namespace Morestachio.Helper
{
	/// <summary>
	///		Can be returned by a Formatter to control what formatter should be used
	/// </summary>
	public enum FormatterFlow
	{
		/// <summary>
		///		Stop the execution and try another formatter
		/// </summary>
		Skip,
	}

	/// <summary>
	///		Matches the Arguments from the Template to a Function from .net
	/// </summary>
	public class FormatterMatcher
	{
		/// <summary>
		/// 
		/// </summary>
		public FormatterMatcher()
		{
			Formatter = new List<FormatTemplateElement>();
		}

		/// <summary>
		///		The Enumeration of all formatter
		/// </summary>
		[NotNull]
		[ItemNotNull]
		public ICollection<FormatTemplateElement> Formatter { get; private set; }

		/// <summary>
		/// Gets the matching formatter.
		/// </summary>
		/// <param name="typeToFormat">The type to format.</param>
		/// <returns></returns>
		[CanBeNull]
		public virtual IEnumerable<FormatTemplateElement> GetMatchingFormatter([NotNull]Type typeToFormat)
		{
			yield return Formatter.FirstOrDefault(e => typeToFormat == e.InputTypes);
			foreach (var formatTemplateElement in Formatter.Where(e => e.InputTypes.IsAssignableFrom(typeToFormat)))
			{
				yield return formatTemplateElement;
			}
		}

		/// <summary>
		/// Adds the formatter.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="formatterDelegate">The formatter delegate.</param>
		public virtual void AddFormatter<T>([NotNull]Delegate formatterDelegate)
		{
			AddFormatter(typeof(T), formatterDelegate);
		}

		/// <summary>
		/// Adds the formatter.
		/// </summary>
		/// <param name="forType">For type.</param>
		/// <param name="formatterDelegate">The formatter delegate.</param>
		public virtual void AddFormatter([NotNull]Type forType, [NotNull]Delegate formatterDelegate)
		{
			var arguments = formatterDelegate.Method.GetParameters().Select((e, index) =>
				new MultiFormatterInfo(e.ParameterType, e.GetCustomAttribute<FormatterArgumentNameAttribute>()?.Name ?? e.Name, e.IsOptional, index)
				{
					IsSourceObject = e.GetCustomAttribute<SourceObjectAttribute>() != null,
				}).ToArray();

			var returnValue = formatterDelegate.Method.ReturnParameter?.ParameterType;

			//if there is no declared SourceObject then check if the first object is of type what we are formatting and use this one.
			if (!arguments.Any(e => e.IsSourceObject) && arguments.Any() && arguments[0].Type.IsAssignableFrom(forType))
			{
				arguments[0].IsSourceObject = true;
			}

			var sourceValue = arguments.FirstOrDefault(e => e.IsSourceObject);
			if (sourceValue != null)
			{
				//if we have a source value in the arguments reduce the index of all following 
				//this is important as the source value is never in the formatter string so we will not "count" it 
				for (int i = sourceValue.Index; i < arguments.Length; i++)
				{
					arguments[i].Index--;
				}

				sourceValue.Index = -1;
			}

			FormatTemplateElement formatter = null;
			formatter = new FormatTemplateElement(
				formatterDelegate,
				forType,
				returnValue,
				arguments);

			Formatter.Add(formatter);
		}

		/// <summary>
		///		Composes the values into a Dictionary for each formatter. If returns null, the formatter will not be called.
		/// </summary>
		/// <param name="formatter">The formatter.</param>
		/// <param name="sourceObject">The source object.</param>
		/// <param name="templateArguments">The template arguments.</param>
		/// <returns></returns>
		public virtual IDictionary<MultiFormatterInfo, object> ComposeValues([NotNull]FormatTemplateElement formatter,
			[CanBeNull]object sourceObject, [NotNull] params KeyValuePair<string, object>[] templateArguments)
		{
			var values = new Dictionary<MultiFormatterInfo, object>();

			foreach (var multiFormatterInfo in formatter.MetaData)
			{
				object givenValue;
				//set ether the source object or the value from the given arguments
				if (multiFormatterInfo.IsSourceObject)
				{
					givenValue = sourceObject;
				}
				else
				{
					//match by index or name
					var index = 0;
					givenValue = templateArguments.FirstOrDefault((g) =>
					{
						if (!String.IsNullOrWhiteSpace(g.Key))
						{
							index++;
							return g.Key.Equals(multiFormatterInfo.Name);
						}
						return (index++) == multiFormatterInfo.Index;
					}).Value;
				}

				values.Add(multiFormatterInfo, givenValue);
				if (multiFormatterInfo.IsOptional || multiFormatterInfo.IsSourceObject)
				{
					continue; //value and source object are optional so we do not to check for its existence 
				}

				if (Equals(givenValue, null))
				{
					//the delegates parameter is not optional so this formatter does not fit. Continue.
					return null;
				}
			}

			return values;
		}

		/// <summary>
		/// Executes the specified formatter.
		/// </summary>
		/// <param name="formatter">The formatter.</param>
		/// <param name="sourceObject">The source object.</param>
		/// <param name="templateArguments">The template arguments.</param>
		/// <returns></returns>
		public virtual object Execute([NotNull]FormatTemplateElement formatter,
			[CanBeNull]object sourceObject,
			params KeyValuePair<string, object>[] templateArguments)
		{
			var values = ComposeValues(formatter, sourceObject, templateArguments);

			if (values == null)
			{
				return sourceObject;
			}

			return formatter.Format.DynamicInvoke(values.Select(e => e.Value).ToArray());
		}

		/// <summary>
		///     Gets the Formatter that matches the given type most from ether the Options.Formatter or the global or null
		/// </summary>
		/// <param name="type"></param>
		/// <param name="additionalFormatters"></param>
		/// <returns></returns>
		public IEnumerable<FormatTemplateElement> GetMostMatchingFormatter(Type type)
		{
			if (type == null)
			{
				return GetMatchingFormatter(typeof(object));
			}
			return GetMatchingFormatter(type);
		}

		/// <summary>
		///		Searches for the first formatter does not reject the value.
		/// </summary>
		/// <param name="type">The type.</param>
		/// <param name="arguments">The arguments.</param>
		/// <param name="value">The value.</param>
		/// <returns></returns>
		public object CallMostMatchingFormatter(Type type, KeyValuePair<string, object>[] arguments, object value)
		{
			var hasFormatter = GetMostMatchingFormatter(type).Where(e => e != null);

			foreach (var formatTemplateElement in hasFormatter)
			{
				var executeFormatter = Execute(formatTemplateElement, value, arguments);
				if ((executeFormatter as FormatterFlow?) != FormatterFlow.Skip)
				{
					return executeFormatter;
				}
			}

			return FormatterFlow.Skip;
		}
	}
}
