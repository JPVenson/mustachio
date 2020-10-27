﻿using System.Linq;
using System.Runtime.Serialization;
using Morestachio.Document;
using Morestachio.Document.Contracts;
using Morestachio.Document.Items.Base;
using Morestachio.Document.Visitor;
using Morestachio.Framework;
using Morestachio.Framework.Context;
using Morestachio.Framework.Expression;
using Morestachio.Framework.IO;
#if ValueTask
using ItemExecutionPromise = System.Threading.Tasks.ValueTask<System.Collections.Generic.IEnumerable<Morestachio.Document.Contracts.DocumentItemExecution>>;
#else
using ItemExecutionPromise = System.Threading.Tasks.Task<System.Collections.Generic.IEnumerable<Morestachio.Document.Contracts.DocumentItemExecution>>;
#endif
namespace Morestachio.Helper.Localization.Documents.LocPDocument
{
	/// <summary>
	///		Allows the usage of {{#loc expression}} in combination with an <see cref="IMorestachioLocalizationService"/>
	/// </summary>
	[System.Serializable]
	public class MorestachioLocalizationParameterDocumentItem : ExpressionDocumentItemBase,
		ToParsableStringDocumentVisitor.IStringVisitor
	{
		internal MorestachioLocalizationParameterDocumentItem() : base(CharacterLocation.Unknown, null)
		{

		}

		/// <inheritdoc />
		public MorestachioLocalizationParameterDocumentItem(CharacterLocation location, IMorestachioExpression value) : base(location, value)
		{
		}

		/// <inheritdoc />
		protected MorestachioLocalizationParameterDocumentItem(SerializationInfo info, StreamingContext c) : base(info, c)
		{
		}

		/// <inheritdoc />
		public override ItemExecutionPromise Render(IByteCounterStream outputStream,
			ContextObject context,
			ScopeData scopeData)
		{
			return Enumerable.Empty<DocumentItemExecution>()
				.ToPromise();
		}

		/// <inheritdoc />
		public override void Accept(IDocumentItemVisitor visitor)
		{
			visitor.Visit(this);
		}

		/// <inheritdoc />
		public void Render(ToParsableStringDocumentVisitor visitor)
		{
			visitor.StringBuilder.Append("{{" + MorestachioLocalizationParamTagProvider.OpenTag + visitor.ReparseExpression(MorestachioExpression) + "}}");
		}
	}
}