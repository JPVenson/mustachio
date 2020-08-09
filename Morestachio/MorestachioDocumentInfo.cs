﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Morestachio.Document;
using Morestachio.Document.Contracts;
using Morestachio.Framework;
using Morestachio.Helper;
using Morestachio.ParserErrors;

#if ValueTask
using MorestachioDocumentResultPromise = System.Threading.Tasks.ValueTask<Morestachio.MorestachioDocumentResult>;
using StringPromise = System.Threading.Tasks.ValueTask<string>;
#else
using MorestachioDocumentResultPromise = System.Threading.Tasks.Task<Morestachio.MorestachioDocumentResult>;
using StringPromise = System.Threading.Tasks.Task<string>;
#endif

namespace Morestachio
{
	/// <summary>
	///     Provided when parsing a template and getting information about the embedded variables.
	/// </summary>
	public class MorestachioDocumentInfo
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MorestachioDocumentInfo"/> class.
		/// </summary>
		/// <param name="options">The options.</param>
		/// <param name="document">The document.</param>
		public MorestachioDocumentInfo([NotNull] ParserOptions options, [NotNull] IDocumentItem document)
			: this(options, document ?? throw new ArgumentNullException(nameof(document)), null)
		{

		}

		internal MorestachioDocumentInfo([NotNull]ParserOptions options, [CanBeNull]IDocumentItem document, [CanBeNull]IEnumerable<IMorestachioError> errors)
		{
			ParserOptions = options ?? throw new ArgumentNullException(nameof(options));
			Document = document;
			Errors = errors ?? Enumerable.Empty<IMorestachioError>();
		}

		/// <summary>
		///		The Morestachio Document generated by the <see cref="Parser"/>
		/// </summary>
		[CanBeNull]
		public IDocumentItem Document { get; }

		/// <summary>
		///     The parser Options object that was used to create the Template Delegate
		/// </summary>
		[NotNull]
		public ParserOptions ParserOptions { get; }

		/// <summary>
		///		Gets a list of errors occured while parsing the Template
		/// </summary>
		[NotNull]
		public IEnumerable<IMorestachioError> Errors { get; private set; }

		internal const int BufferSize = 2024;

		/// <summary>
		///     Calls the Underlying Template Delegate and Produces a Stream
		/// </summary>
		/// <param name="data"></param>
		/// <param name="token"></param>
		/// <returns></returns>
		[MustUseReturnValue("The Stream contains the template. Use CreateAndStringify() to get the string of it")]
		[NotNull]
		public async MorestachioDocumentResultPromise CreateAsync([NotNull]object data, CancellationToken token)
		{
			if (Errors.Any())
			{
				throw new AggregateException("You cannot Create this Template as there are one or more Errors. See Inner Exception for more infos.", Errors.Select(e => e.GetException())).Flatten();
			}

			if (Document is MorestachioDocument morestachioDocument && morestachioDocument.MorestachioVersion !=
				MorestachioDocument.GetMorestachioVersion())
			{
				throw new InvalidOperationException($"The supplied version in the Morestachio document " +
													$"'{morestachioDocument.MorestachioVersion}'" +
													$" is not compatible with the current morestachio version of " +
													$"'{MorestachioDocument.GetMorestachioVersion()}'");
			}

			var timeoutCancellation = new CancellationTokenSource();
			if (ParserOptions.Timeout != TimeSpan.Zero)
			{
				timeoutCancellation.CancelAfter(ParserOptions.Timeout);
				var anyCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCancellation.Token);
				token = anyCancellationToken.Token;
			}
			PerformanceProfiler profiler = null;
			using (var byteCounterStream = ParserOptions.StreamFactory.GetByteCounterStream(ParserOptions))
			{
				if (byteCounterStream == null)
				{
					throw new NullReferenceException("The created stream is null.");
				}
				var context = ParserOptions.CreateContextObject("", token, data);
				using (var scopeData = new ScopeData())
				{
					if (ParserOptions.ProfileExecution)
					{
						scopeData.Profiler = profiler = new PerformanceProfiler(true);
					}
					await MorestachioDocument.ProcessItemsAndChildren(new[] { Document }, byteCounterStream,
						context, scopeData);
				}

				if (timeoutCancellation.IsCancellationRequested)
				{
					throw new TimeoutException($"The requested timeout of '{ParserOptions.Timeout:g}' for template generation was reached.");
				}
				return new MorestachioDocumentResult()
				{
					Stream = byteCounterStream.Stream,
					Profiler = profiler
				};
			}
		}

		/// <summary>
		///     Calls the Underlying Template Delegate and Produces a Stream
		/// </summary>
		/// <param name="data"></param>
		/// <returns></returns>
		[MustUseReturnValue("The Stream contains the template. Use CreateAndStringify() to get the string of it")]
		[NotNull]
		[PublicAPI]
		public async MorestachioDocumentResultPromise CreateAsync([NotNull]object data)
		{
			return await CreateAsync(data, CancellationToken.None);
		}

		/// <summary>
		///     Calls the Underlying Template Delegate and Produces a Stream
		/// </summary>
		/// <param name="source"></param>
		/// <returns></returns>
		[NotNull]
		[PublicAPI]
		public async StringPromise CreateAndStringifyAsync([NotNull]object source)
		{
			return await CreateAndStringifyAsync(source, CancellationToken.None);
		}

		/// <summary>
		///     Calls the Underlying Template Delegate and Produces a Stream
		/// </summary>
		/// <param name="source"></param>
		/// <param name="token"></param>
		/// <returns></returns>
		[NotNull]
		[PublicAPI]
		public async StringPromise CreateAndStringifyAsync([NotNull]object source, CancellationToken token)
		{
			using (var stream = (await CreateAsync(source, token)).Stream)
			{
				return stream.Stringify(true, ParserOptions.Encoding);
			}
		}

		/// <summary>
		///     Calls the Underlying Template Delegate and Produces a Stream
		/// </summary>
		/// <param name="source"></param>
		/// <param name="token"></param>
		/// <returns></returns>
		[MustUseReturnValue("The Stream contains the template. Use CreateAndStringify() to get the string of it")]
		[NotNull]
		[PublicAPI]
		public MorestachioDocumentResult Create([NotNull]object source, CancellationToken token)
		{
			return CreateAsync(source, token).ConfigureAwait(false).GetAwaiter().GetResult();
		}

		/// <summary>
		///     Calls the Underlying Template Delegate and Produces a Stream
		/// </summary>
		/// <param name="source"></param>
		/// <returns></returns>
		[MustUseReturnValue("The Stream contains the template. Use CreateAndStringify() to get the string of it")]
		[NotNull]
		[PublicAPI]
		public MorestachioDocumentResult Create([NotNull]object source)
		{
			return Create(source, CancellationToken.None);
		}

		/// <summary>
		///     Calls the Underlying Template Delegate and Produces a Stream
		/// </summary>
		/// <param name="source"></param>
		/// <returns></returns>
		[NotNull]
		[PublicAPI]
		public string CreateAndStringify([NotNull]object source)
		{
			return CreateAndStringify(source, CancellationToken.None);
		}

		/// <summary>
		///     Calls the Underlying Template Delegate and Produces a Stream
		/// </summary>
		/// <param name="source"></param>
		/// <param name="token"></param>
		/// <returns></returns>
		[NotNull]
		[PublicAPI]
		public string CreateAndStringify([NotNull]object source, CancellationToken token)
		{
			using (var stream = Create(source, token).Stream)
			{
				return stream.Stringify(true, ParserOptions.Encoding);
			}
		}
	}
}