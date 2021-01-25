﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Morestachio.Formatter.Framework;
using Morestachio.Framework;
using Morestachio.Framework.Error;
using Morestachio.Linq;
using NUnit.Framework;

namespace Morestachio.Tests
{
	[TestFixture(ParserOptionTypes.UseOnDemandCompile)]
	[TestFixture(ParserOptionTypes.Precompile)]
	[Parallelizable(ParallelScope.All)]
	public class PartialFixture
	{
		private readonly ParserOptionTypes _options;

		public PartialFixture(ParserOptionTypes options)
		{
			_options = options;
		}


		[Test]
		public async Task ParserCanPartials()
		{
			var template =
				@"{{#DECLARE TestPartial}}{{self.Test}}{{/DECLARE}}{{#EACH Data}}{{#IMPORT 'TestPartial'}}{{/EACH}}";
			var data = new Dictionary<string, object>();
			data["Data"] = new List<object>
			{
				new Dictionary<string, object>
				{
					{
						"self", new Dictionary<string, object>
						{
							{"Test", 1}
						}
					}
				},
				new Dictionary<string, object>
				{
					{
						"self", new Dictionary<string, object>
						{
							{"Test", 2}
						}
					}
				},
				new Dictionary<string, object>
				{
					{
						"self", new Dictionary<string, object>
						{
							{"Test", 3}
						}
					}
				}
			};

			var result = await ParserFixture.CreateAndParseWithOptions(template, data, _options, options =>
			{
				options.Formatters.AddFromType(typeof(ParserFixture.NumberFormatter));
			});

			Assert.That(result, Is.EqualTo("123"));
		}

		[Test]
		public async Task ParserCanIncludePartialsWithExplicitScope()
		{
			var data = new Dictionary<string, object>();
			data["Data"] = new List<object>
			{
				new Dictionary<string, object>
				{
					{
						"self", new Dictionary<string, object>
						{
							{"Test", 1}
						}
					}
				},
				new Dictionary<string, object>
				{
					{
						"self", new Dictionary<string, object>
						{
							{"Test", 2}
						}
					}
				},
			};

			var template =
				@"{{#DECLARE TestPartial}}{{self.Test}}{{/DECLARE}}{{#IMPORT 'TestPartial' #WITH Data.ElementAt(1)}}";


			var result = await ParserFixture.CreateAndParseWithOptions(template, data, _options, options =>
			{
				options.Formatters.AddFromType(typeof(ParserFixture.NumberFormatter));
			});
			Assert.That(result, Is.EqualTo("2"));
		}

		[Test]
		public async Task ParserCanIncludePartialsWithExplicitScopeFromFormatter()
		{
			var data = new Dictionary<string, object>();
			data["Data"] = new List<object>
			{
				new Dictionary<string, object>
				{
					{
						"self", new Dictionary<string, object>
						{
							{"Test", 1}
						}
					}
				},
				new Dictionary<string, object>
				{
					{
						"self", new Dictionary<string, object>
						{
							{"Test", 2}
						}
					}
				},
			};

			var template =
				@"{{#DECLARE TestPartial}}{{ExportedValue.ElementAt(1).self.Test}}{{/DECLARE}}{{#IMPORT 'TestPartial' #WITH Data.Self($name)}}";

			var result = await ParserFixture.CreateAndParseWithOptions(template, data, _options, options =>
			{
				options.Formatters.AddFromType(typeof(DynamicLinq));

				options.Formatters.AddSingle(new Func<object, string, object>((sourceObject, name) =>
				{
					return new Dictionary<string, object>()
					{
						{"ExportedValue", sourceObject},
						{"XNAME", name}
					};
				}), "Self");
			});

			Assert.That(result, Is.EqualTo("2"));
		}

		[Test]
		public async Task ParserCanIncludePartialsWithExplicitScopeAndDynamicImport()
		{
			var data = new Dictionary<string, object>();
			data["Data"] = new List<object>
			{
				new Dictionary<string, object>
				{
					{
						"self", new Dictionary<string, object>
						{
							{"Test", 1}
						}
					}
				},
				new Dictionary<string, object>
				{
					{
						"self", new Dictionary<string, object>
						{
							{"Test", 2}
						}
					}
				},
			};

			var template =
				"{{#DECLARE TestPartial}}" +
				"{{self.Test}}" +
				"{{/DECLARE}}" +
				"{{#VAR partialName = 'TestPartial'}}" +
				"{{#IMPORT partialName #WITH Data.ElementAt(1)}}";


			var result = await ParserFixture.CreateAndParseWithOptions(template, data, _options);

			Assert.That(result, Is.EqualTo("2"));
		}

		[Test]
		public async Task ParserCanPartialsOneUp()
		{
			var data = new Dictionary<string, object>();
			data["Data"] = new List<object>
			{
				new Dictionary<string, object>
				{
					{
						"self", new Dictionary<string, object>
						{
							{"Test", 1}
						}
					}
				},
				new Dictionary<string, object>
				{
					{
						"self", new Dictionary<string, object>
						{
							{"Test", 2}
						}
					}
				}
			};

			data["DataOneUp"] =
				new Dictionary<string, object>
				{
					{
						"self", new Dictionary<string, object>
						{
							{"Test", "Is:"}
						}
					}
				};

			var template =
				"{{#DECLARE TestPartial}}{{../../DataOneUp.self.Test}}{{self.Test}}{{/DECLARE}}" +
				"{{#EACH Data}}" +
				"	{{#IMPORT 'TestPartial'}}" +
				"{{/EACH}}";

			var result = await ParserFixture.CreateAndParseWithOptions(template, data, _options);
			Assert.That(result, Is.EqualTo("	Is:1	Is:2"));
		}

		[Test]
		public void ParserThrowsOnInfiniteNestedCalls()
		{
			var data = new Dictionary<string, object>();
			var template = @"{{#declare TestPartial}}{{#IMPORT 'TestPartial'}}{{/declare}}{{#IMPORT 'TestPartial'}}";

			var parsingOptions = new ParserOptions(template, null, ParserFixture.DefaultEncoding);
			var parsedTemplate = Parser.ParseWithOptions(parsingOptions);
			ParserFixture.TestLocationsInOrder(parsedTemplate);
			Assert.That(async () => await parsedTemplate.CreateAndStringifyAsync(data),
				Throws.Exception.TypeOf<MustachioStackOverflowException>());
			SerilalizerTests.SerializerTest.AssertDocumentItemIsSameAsTemplate(parsingOptions.Template, parsedTemplate.Document, parsingOptions);
		}

		[Test]
		public async Task ParserCanCreateNestedPartials()
		{
			var data = new Dictionary<string, object>();
			var template =
				@"{{#declare TestPartial}}{{#declare InnerPartial}}1{{/declare}}2{{/declare}}{{#IMPORT 'TestPartial'}}{{#IMPORT 'InnerPartial'}}";

			var result = await ParserFixture.CreateAndParseWithOptions(template, data, _options);
			Assert.That(result, Is.EqualTo("21"));
		}

		[Test]
		public async Task ParserCanPrintNested()
		{
			var data = new Dictionary<string, object>();
			//declare TestPartial -> Print Recursion -> If Recursion is smaller then 10 -> Print TestPartial
			//Print TestPartial
			var template =
				@"{{#declare TestPartial}}{{$recursion}}{{#SCOPE $recursion.() as rec}}{{#IMPORT 'TestPartial'}}{{/SCOPE}}{{/declare}}{{#IMPORT 'TestPartial'}}";

			var result = await ParserFixture.CreateAndParseWithOptions(template, data, _options, options =>
			{
				options.Formatters.AddSingle<int, bool>(e => { return e < 9; });
			});
			Assert.That(result, Is.EqualTo("123456789"));
		}

		[Test]
		public async Task ParserCanLoadFileStore()
		{
			var tempPath = Path.Combine(Path.GetTempPath(), "MorestachioTesting", Environment.Version.ToString(), _options.GetHashCode().ToString());
			Directory.CreateDirectory(tempPath);

			var data = new Dictionary<string, object>()
			{
				{
					"data", new Dictionary<string, object>()
					{
						{"name", "Bond"}
					}
				}
			};
			try
			{
				File.WriteAllText(Path.Combine(tempPath, "content.html"), "Hello World", ParserFixture.DefaultEncoding);
				File.WriteAllText(Path.Combine(tempPath, "instruction.html"), "Hello mr {{data.name}}", ParserFixture.DefaultEncoding);
				Directory.CreateDirectory(Path.Combine(tempPath, "sub"));
				File.WriteAllText(Path.Combine(tempPath, "sub", "base.html"), "Sub Path", ParserFixture.DefaultEncoding);
				
				var template =
					@"Blank
{{#IMPORT 'File/content'}}
{{#IMPORT 'File/instruction'}}
{{#IMPORT 'File/base'}}
";

				var result = await ParserFixture.CreateAndParseWithOptions(template, data, _options, options =>
				{
					options.PartialsStore = new FileSystemPartialStore(tempPath, "*.html", true, true, "File/");
				});
				Assert.That(result, Is.EqualTo(@"Blank
Hello World
Hello mr Bond
Sub Path
"));
			}
			finally
			{
				Directory.Delete(tempPath, true);
			}
		}
	}
}
