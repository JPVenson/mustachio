﻿#if ValueTask
using ItemExecutionPromise = System.Threading.Tasks.ValueTask<System.Collections.Generic.IEnumerable<Morestachio.Document.Contracts.DocumentItemExecution>>;
using Promise = System.Threading.Tasks.ValueTask;
#else
using ItemExecutionPromise = System.Threading.Tasks.Task<System.Collections.Generic.IEnumerable<Morestachio.Document.Contracts.DocumentItemExecution>>;
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Xml;
using Morestachio.Document.Contracts;
using Morestachio.Document.Items.Base;
using Morestachio.Document.TextOperations;
using Morestachio.Document.Visitor;
using Morestachio.Framework;
using Morestachio.Framework.Context;
using Morestachio.Framework.Context.Options;
using Morestachio.Framework.IO;
using Morestachio.Framework.Tokenizing;
using Morestachio.Helper;

namespace Morestachio.Document.Items
{
	/// <summary>
	/// 
	/// </summary>
	[Serializable]
	public class TextEditDocumentItem : DocumentItemBase, ISupportCustomCompilation
	{
		/// <summary>
		///		The TextOperation
		/// </summary>
		public ITextOperation Operation { get; private set; }

		/// <summary>
		///		If set to true, indicates that this text operation is used as an appendix or suffix to another keyword
		/// </summary>
		public EmbeddedInstructionOrigin EmbeddedInstructionOrigin { get; private set; }

		internal TextEditDocumentItem()
		{

		}

		/// <summary>
		/// 
		/// </summary>
		public TextEditDocumentItem(CharacterLocation location,
			 ITextOperation operation,
			EmbeddedInstructionOrigin embeddedInstructionOrigin,
			IEnumerable<ITokenOption> tagCreationOptions)
			: base(location, tagCreationOptions)
		{
			Operation = operation ?? throw new ArgumentNullException(nameof(operation));
			EmbeddedInstructionOrigin = embeddedInstructionOrigin;
		}

		/// <inheritdoc />
		
		protected TextEditDocumentItem(SerializationInfo info, StreamingContext c) : base(info, c)
		{
			EmbeddedInstructionOrigin = (EmbeddedInstructionOrigin)info.GetValue(nameof(EmbeddedInstructionOrigin), typeof(EmbeddedInstructionOrigin));
			Operation = info.GetValue(nameof(Operation), typeof(ITextOperation)) as ITextOperation;
		}

		/// <param name="compiler"></param>
		/// <inheritdoc />
		public Compilation Compile(IDocumentCompiler compiler)
		{
			return (outputStream, context, scopeData) =>
			{
				CoreAction(outputStream, scopeData);
			};
		}

		/// <inheritdoc />
		public override ItemExecutionPromise Render(
			IByteCounterStream outputStream,
			ContextObject context,
			ScopeData scopeData)
		{
			CoreAction(outputStream, scopeData);
			return Enumerable.Empty<DocumentItemExecution>().ToPromise();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void CoreAction(IByteCounterStream outputStream, ScopeData scopeData)
		{
			if (Operation.IsModificator)
			{
				throw new MorestachioRuntimeException("Cannot execute a Text-Modification on its own.");
			}
			else
			{
				outputStream.Write(Operation.Apply(string.Empty));
			}
		}

		/// <inheritdoc />
		protected override void SerializeBinaryCore(SerializationInfo info, StreamingContext context)
		{
			base.SerializeBinaryCore(info, context);
			info.AddValue(nameof(Operation), Operation);
			info.AddValue(nameof(EmbeddedInstructionOrigin), EmbeddedInstructionOrigin);
		}

		/// <inheritdoc />
		protected override void SerializeXml(XmlWriter writer)
		{
			base.SerializeXml(writer);
			writer.WriteStartElement("TextOperation");
			writer.WriteAttributeString(nameof(ITextOperation.TextOperationType), Operation.TextOperationType.ToString());
			writer.WriteAttributeString(nameof(EmbeddedInstructionOrigin), EmbeddedInstructionOrigin.ToString());
			Operation.WriteXml(writer);
			writer.WriteEndElement();//</TextOperation>
		}

		/// <inheritdoc />
		protected override void DeSerializeXml(XmlReader reader)
		{
			base.DeSerializeXml(reader);
			reader.ReadStartElement();//<TextOperation>
			AssertElement(reader, "TextOperation");
			var embeddedState = reader.GetAttribute(nameof(EmbeddedInstructionOrigin));
			if (!string.IsNullOrEmpty(embeddedState))
			{
				EmbeddedInstructionOrigin = (EmbeddedInstructionOrigin)Enum.Parse(typeof(EmbeddedInstructionOrigin), embeddedState);
			}

			var attribute = reader.GetAttribute(nameof(ITextOperation.TextOperationType));
			switch (attribute)
			{
				case "LineBreak":
					Operation = new AppendLineBreakTextOperation();
					break;
				case "TrimLineBreaks":
					Operation = new TrimLineBreakTextOperation();
					break;
				default:
					throw new InvalidOperationException($"The TextOperation '{attribute}' is invalid");
			}

			Operation.ReadXml(reader);
			reader.ReadEndElement();//</TextOperation>
		}

		/// <inheritdoc />
		public override void Accept(IDocumentItemVisitor visitor)
		{
			visitor.Visit(this);
		}
	}
}
