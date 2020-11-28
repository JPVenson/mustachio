﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Morestachio.Framework;
using Morestachio.Helper;
using Morestachio.Parsing.ParserErrors;
using NUnit.Framework;

namespace Morestachio.Tests
{
	[TestFixture(ParserOptionTypes.UseOnDemandCompile)]
	[TestFixture(ParserOptionTypes.Precompile)]
	[Parallelizable(ParallelScope.All)]
	public class TemplateFixture
	{
		private readonly ParserOptionTypes _options;

		public TemplateFixture(ParserOptionTypes options)
		{
			_options = options;
		}

		[Test]
		[TestCase(200)]
		[TestCase(80000)]
		[TestCase(700000)]
		public async Task TemplateMaxSizeLimit(int maxSize)
		{
			var dataValue = new List<string>();
			for (var i = 0; i < maxSize / ParserFixture.DefaultEncoding.GetByteCount(" "); i++)
			{
				dataValue.Add(" ");
			}

			var template = "{{#each Data}}{{.}}{{/each}}";
			var data = new Dictionary<string, object>
			{
				{"Data", dataValue}
			};
			var result = await ParserFixture.CreateAndParseWithOptionsStream(template, data, _options, options =>
			{
				options.MaxSize = maxSize;
			});
			Assert.AreEqual(result.Length, maxSize);
		}

		[Test]
		[TestCase(6)]
		[TestCase(7)]
		[TestCase(8)]
		[TestCase(200)]
		[TestCase(80000)]
		[TestCase(700000)]
		public async Task TemplateMaxSizeOverLimit(int maxSize)
		{
			var dataValue = new List<string>();
			var sizeOfOneChar = ParserFixture.DefaultEncoding.GetByteCount(" ");
			for (var i = 0; i < (maxSize / sizeOfOneChar) + sizeOfOneChar; i++)
			{
				dataValue.Add(" ");
			}

			var template = "{{#each Data}}{{.}}{{/each}}";
			var data = new Dictionary<string, object>
			{
				{"Data", dataValue}
			};
			var result = await ParserFixture.CreateAndParseWithOptionsStream(template, data, _options, options =>
			{
				options.MaxSize = maxSize;
			});
			Assert.That(result.Length, Is.EqualTo(maxSize).Or.EqualTo(maxSize - 1));
		}

		[TestCase(new int[] { })]
		[TestCase(false)]
		[TestCase("")]
		[TestCase(0.0)]
		[TestCase(0)]
		[Test]
		public async Task TemplatesShoudlNotRenderFalseyComplexStructures(object falseyModelValue)
		{
			var data = new Dictionary<string, object>
			{
				{"outer_level", falseyModelValue}
			};

			var template = "{{#outer_level}}Shouldn't be rendered!{{inner_level}}{{/outer_level}}";

			var result = await ParserFixture.CreateAndParseWithOptions(template, data, _options);
			Assert.That(result, Is.EqualTo(string.Empty));
		}

		[TestCase(new int[] { })]
		[TestCase(false)]
		[TestCase("")]
		[TestCase(0.0)]
		[TestCase(0)]
		[Test]
		public async Task TemplateShouldTreatFalseyValuesAsEmptyArray(object falseyModelValue)
		{
			var data = new Dictionary<string, object>
			{
				{"locations", falseyModelValue}
			};

			var template = "{{#each locations}}Shouldn't be rendered!{{/each}}";

			var result = await ParserFixture.CreateAndParseWithOptions(template, data, _options);
			Assert.That(result, Is.EqualTo(string.Empty));
		}

		[TestCase(0)]
		[TestCase(0.0)]
		[Test]
		public async Task TemplateShouldRenderZeroValue(object value)
		{
			var data = new Dictionary<string, object>
			{
				{"times_won", value}
			};

			var template = "You've won {{times_won}} times!";

			var result = await ParserFixture.CreateAndParseWithOptions(template, data, _options);
			Assert.That(result, Is.EqualTo("You've won 0 times!"));
		}

		[Test]
		public async Task CommentsAreExcludedFromOutput()
		{
			var data = new Dictionary<string, object>();
			var template = @"as{{!stu
			ff}}df";
			var result = await ParserFixture.CreateAndParseWithOptions(template, data, _options | ParserOptionTypes.NoRerenderingTest);
			Assert.That(result, Is.EqualTo("asdf"));
		}

		[Test]
		public async Task TestMultiLineCommentsAreExcludedFromOutput()
		{
			var data = new Dictionary<string, object>();
			var template = @"A{{!}}ZZZ{{/!}}B{{!}} {{123}} {{'{{'}} {{'}} }} {{/!}}C";
			var result = await ParserFixture.CreateAndParseWithOptions(template, data, _options | ParserOptionTypes.NoRerenderingTest);
			Assert.That(result, Is.EqualTo("ABC"));
		}

		[Test]
		public async Task HtmlIsEscapedByDefault()
		{
			var data = new Dictionary<string, object>()
			{
				{"stuff", "<b>inner</b>"}
			};

			var template = @"{{stuff}}";
			var result = await ParserFixture.CreateAndParseWithOptions(template, data, _options);
			Assert.That(result, Is.EqualTo("&lt;b&gt;inner&lt;/b&gt;"));
		}

		[Test]
		public async Task HtmlIsNotEscapedWhenUsingUnsafeSyntaxes()
		{
			var data = new Dictionary<string, object>()
			{
				{"stuff", "<b>inner</b>"}
			};

			var template = @"{{&stuff}}";
			var result = await ParserFixture.CreateAndParseWithOptions(template, data, _options);
			Assert.That(result, Is.EqualTo("<b>inner</b>"));
		}

		[Test]
		public async Task NegationGroupRendersContentWhenValueNotSet()
		{
			var data = new Dictionary<string, object>();
			var template = @"{{^stuff}}No Stuff Here.{{/stuff}}";
			var result = await ParserFixture.CreateAndParseWithOptions(template, data, _options);
			Assert.That(result, Is.EqualTo("No Stuff Here."));
		}

		[Test]
		public async Task TemplateRendersContentWithNoVariables()
		{
			var data = new Dictionary<string, object>();
			var template = @"ASDF";
			var result = await ParserFixture.CreateAndParseWithOptions(template, data, _options);
			Assert.That(result, Is.EqualTo("ASDF"));
		}


		[Test]
		public async Task TemplateRendersWithComplexEachPath()
		{
			var template =
				@"{{#each Company.ceo.products}}<li>{{name}} and {{version}} and has a CEO: {{../../last_name}}</li>{{/each}}";

			var data = new Dictionary<string, object>()
			{
				{
					"Company", new Dictionary<string, object>()
					{
						{
							"ceo", new Dictionary<string, object>()
							{
								{"last_name", "Smith"},
								{"products", Enumerable.Range(0, 3).Select(k =>
								{
									var r = new Dictionary<string, object>();
									r["name"] = "name " + k;
									r["version"] = "version " + k;
									return r;
								}).ToArray()}
							}
						}
					}
				}
			};

			var result = await ParserFixture.CreateAndParseWithOptions(template, data, _options);
			Assert.That(result, Is.EqualTo("<li>name 0 and version 0 and has a CEO: Smith</li>" +
										   "<li>name 1 and version 1 and has a CEO: Smith</li>" +
										   "<li>name 2 and version 2 and has a CEO: Smith</li>"));
		}

		[Test]
		public async Task TemplateRendersWithComplexScopePath()
		{
			var template =
				@"{{#Company.ceo}}{{#each products}}<li>{{name}} and {{version}} and has a CEO: {{../../last_name}}</li>{{/each}}{{/Company.ceo}}";

			var data = new Dictionary<string, object>()
			{
				{
					"Company", new Dictionary<string, object>()
					{
						{
							"ceo", new Dictionary<string, object>()
							{
								{"last_name", "Smith"},
								{"products", Enumerable.Range(0, 3).Select(k =>
								{
									var r = new Dictionary<string, object>();
									r["name"] = "name " + k;
									r["version"] = "version " + k;
									return r;
								}).ToArray()}
							}
						}
					}
				}
			};



			var result = await ParserFixture.CreateAndParseWithOptions(template, data, _options);
			Assert.That(result, Is.EqualTo("<li>name 0 and version 0 and has a CEO: Smith</li>" +
										   "<li>name 1 and version 1 and has a CEO: Smith</li>" +
										   "<li>name 2 and version 2 and has a CEO: Smith</li>"));

		}

		[Test]
		public async Task TemplateRendersWithComplexRootScopePath()
		{
			var template =
				@"{{#data}}{{~root}}{{/data}}";

			var data = new Dictionary<string, object>()
			{
				{"data", "test" },
				{"root", "tset" }
			};

			var result = await ParserFixture.CreateAndParseWithOptions(template, data, _options);
			Assert.That(result, Is.EqualTo(data["root"]));
		}

		[Test]
		public async Task TemplateIfDoesNotScope()
		{
			var template =
				@"{{#IF data}}{{.}}{{/IF}}";

			var data = new Dictionary<string, object>()
			{
				{"data", "test" },
				{"root", "tset" }
			};

			var result = await ParserFixture.CreateAndParseWithOptions(template, data, _options);
			Assert.That(result, Is.EqualTo(data.ToString()));
		}

		[Test]
		public async Task TemplateIfRendersRootScopePath()
		{
			var template =
				@"{{#IF ~data}}{{data}}{{/IF}}";

			var data = new Dictionary<string, object>()
			{
				{"data", "test" },
				{"root", "tset" }
			};

			var result = await ParserFixture.CreateAndParseWithOptions(template, data, _options);

			Assert.That(result, Is.EqualTo(data["data"]));
		}

		[Test]
		public async Task TemplateIfElse()
		{
			var template =
				@"{{#IF data}}{{data}}{{/IF}}{{#else}}{{root}}{{/else}}";
			var data = new Dictionary<string, object>()
			{
				{"data", "false" },
				{"root", "true" }
			};

			var result = await ParserFixture.CreateAndParseWithOptions(template, data, _options);

			Assert.That(result, Is.EqualTo(data["data"]));
		}

		[Test]
		public async Task TemplateCanExecuteNestedIfs()
		{
			var template =
				@"{{#IF data}}SHOULD PRINT{{#IF alum}}!{{/IF}}{{/IF}}{{#ELSE}}SHOULD NOT PRINT{{/ELSE}}";

			var data = new Dictionary<string, object>()
			{
				{"data", "false" },
				{"root", "true" }
			};

			var result = await ParserFixture.CreateAndParseWithOptions(template, data, _options);

			Assert.That(result, Is.EqualTo("SHOULD PRINT"));
		}

		[Test]
		public async Task TemplateInvertedIfElse()
		{
			var template =
				@"{{^IF data}}{{data}}{{/IF}}{{#else}}{{root}}{{/else}}";

			var data = new Dictionary<string, object>()
			{
				{"data", "false" },
				{"root", "true" }
			};

			var result = await ParserFixture.CreateAndParseWithOptions(template, data, _options);

			Assert.That(result, Is.EqualTo(data["root"]));
		}



		[Test]
		public async Task TemplateIfElseCombined()
		{
			var template =
				@"{{#IF data}}{{data}}{{#ifelse}}{{root}}{{/else}}";

			var data = new Dictionary<string, object>()
			{
				{"data", "false" },
				{"root", "true" }
			};

			var result = await ParserFixture.CreateAndParseWithOptions(template, data, _options | ParserOptionTypes.NoRerenderingTest);

			Assert.That(result, Is.EqualTo(data["data"]));
		}

		[Test]
		public async Task TemplateInvertedIfElseCombined()
		{
			var template =
				@"{{^IF data}}{{data}}{{#ifelse}}{{root}}{{/else}}";

			var data = new Dictionary<string, object>()
			{
				{"data", "false" },
				{"root", "true" }
			};

			var result = await ParserFixture.CreateAndParseWithOptions(template, data, _options | ParserOptionTypes.NoRerenderingTest);

			Assert.That(result, Is.EqualTo(data["root"]));
		}

		[Test]
		public async Task TemplateInvalidContentBetweenIfAndElse()
		{
			var template =
				@"{{^IF data}}{{data}}{{/IF}}{{data}}{{#else}}{{root}}{{/else}}";

			var data = new Dictionary<string, object>()
			{
				{"data", "false" },
				{"root", "true" }
			};
			
			var result = await ParserFixture.CreateAndParseWithOptions(template, data, _options, null, info =>
			{
				Assert.That(info.Errors
					.OfType<MorestachioSyntaxError>()
					.FirstOrDefault(e => e.Location.Equals(CharacterLocation.FromFormatString("1:38"))), Is.Not.Null);
			});
		}

		[Test]
		public async Task TemplateRendersWithComplexRootScopePathInIf()
		{
			var template =
				@"{{#IF data}}{{root}}{{/if}}";

			var data = new Dictionary<string, object>()
			{
				{"data", "test" },
				{"root", "tset" }
			};

			var result = await ParserFixture.CreateAndParseWithOptions(template, data, _options);
			Assert.That(result, Is.EqualTo(data["root"]));
		}

		[Test]
		public async Task TemplateCanRenderSwitchCase()
		{
			var template =
				"{{#SWITCH data}}" +
				"{{#CASE 'tset'}}FAIL{{/CASE}}" +
				"{{#CASE 123}}FAIL{{/CASE}}" +
				"{{#CASE root}}FAIL{{/CASE}}" +
				"{{#CASE 'test'}}SUCCESS{{/CASE}}" +
				"{{/SWITCH}}";

			var data = new Dictionary<string, object>()
			{
				{"data", "test" },
				{"root", "tset" }
			};

			var result = await ParserFixture.CreateAndParseWithOptions(template, data, _options);

			Assert.That(result, Is.EqualTo("SUCCESS"));
		}

		[Test]
		public async Task TemplateCanRenderSwitchCaseWithScopeing()
		{
			var template =
				"{{#SWITCH data #SCOPE}}" +
				"{{#CASE 'tset'}}FAIL-{{.}}{{/CASE}}" +
				"{{#CASE 123}}FAIL-{{.}}{{/CASE}}" +
				"{{#CASE root}}FAIL-{{.}}{{/CASE}}" +
				"{{#CASE 'test'}}SUCCESS-{{.}}{{/CASE}}" +
				"{{/SWITCH}}";

			var data = new Dictionary<string, object>()
			{
				{"data", "test" },
				{"root", "tset" }
			};

			var result = await ParserFixture.CreateAndParseWithOptions(template, data, _options);

			Assert.That(result, Is.EqualTo("SUCCESS-test"));
		}

		[Test]
		public async Task TemplateCanRenderDefaultSwitchCase()
		{
			var template =
				@"{{#SWITCH data}}
				{{#CASE 'tset'}}FAIL{{/CASE}}
				{{#CASE 123}}FAIL{{/CASE}}
				{{#CASE root}}FAIL{{/CASE}}
				{{#DEFAULT}}SUCCESS{{/DEFAULT}}
				{{/SWITCH}}";

			var data = new Dictionary<string, object>()
			{
				{"data", "test" },
				{"root", "tset" }
			};

			var result = await ParserFixture.CreateAndParseWithOptions(template, data, _options);

			Assert.That(result, Is.EqualTo("SUCCESS"));
		}

		[Test]
		public async Task TemplateRendersWithScopeWithAliasPath()
		{
			var template =
				@"{{#data AS test}}{{#~root AS rootTest}}{{test}},{{rootTest}}{{/rootTest}}{{rootTest}}{{/test}}{{test}}";

			var data = new Dictionary<string, object>()
			{
				{"data", "test" },
				{"root", "tset" }
			};

			var result = await ParserFixture.CreateAndParseWithOptions(template, data, _options);

			Assert.That(result, Is.EqualTo(data["data"] + "," + data["root"]));
		}

		[Test]
		public async Task TemplateRendersWithEachWithAliasPath()
		{
			var template =
				@"{{#each data AS dd}}{{dd}}{{/each}}";

			var value = new List<int>()
			{
				1,2,3,4,5
			};
			var data = new Dictionary<string, object>()
			{
				{
					"data", value
				},
			};

			var result = await ParserFixture.CreateAndParseWithOptions(template, data, _options | ParserOptionTypes.NoRerenderingTest);

			Assert.That(result, Is.EqualTo(value.Select(e => e.ToString()).Aggregate((e, f) => e + "" + f)));
		}

		[Test]
		public async Task TemplateRendersWithComplexUpScopePath()
		{
			var template =
				@"{{#Data1.Data2.NullableInit}}{{../../../root}}{{/Data1.Data2.NullableInit}}";

			var data = new Dictionary<string, object>()
			{
				{
					"Data1", new Dictionary<string, object>()
					{
						{
							"Data2", new Dictionary<string, object>()
							{
								{"NullableInit", (int?) 1}
							}
						}
					}
				},
				{"root", "tset"}
			};

			var result = await ParserFixture.CreateAndParseWithOptions(template, data, _options);

			Assert.That(result, Is.EqualTo(data["root"]));
		}

		[Test]
		public async Task TemplateRendersWithComplexRootScopePathWithFormatting()
		{
			var template =
				@"{{#Data1.Data2.NullableInit}}{{~root}}{{/Data1.Data2.NullableInit}}";

			var data = new Dictionary<string, object>()
			{
				{
					"Data1", new Dictionary<string, object>()
					{
						{
							"Data2", new Dictionary<string, object>()
							{
								{"NullableInit", (int?) 1}
							}
						}
					}
				},
				{"root", "tset"}
			};

			//1.ToString("E")

			var result = await ParserFixture.CreateAndParseWithOptions(template, data, _options);

			Assert.That(result, Is.EqualTo(data["root"]));
		}

		[Test]
		public async Task TemplateRendersWithComplexUpScopePathWithFormatting()
		{
			var template =
				@"{{#d.d.n}}{{../../../r.('c')}}{{/d.d.n}}";

			var data = new Dictionary<string, object>()
			{
				{
					"d", new Dictionary<string, object>()
					{
						{
							"d", new Dictionary<string, object>()
							{
								{"n", (int?) 1}
							}
						}
					}
				},
				{"r", "tset"}
			};

			var result = await ParserFixture.CreateAndParseWithOptions(template, data, _options);

			Assert.That(result, Is.EqualTo(data["r"]));
		}

		[Test]
		public async Task TemplateShouldNotRenderNullValue()
		{
			var data = new Dictionary<string, object>
			{
				{"times_won", null}
			};

			var template = "You've won {{times_won}} times!";


			var result = await ParserFixture.CreateAndParseWithOptions(template, data, _options);

			Assert.That(result, Is.EqualTo("You've won  times!"));
		}

		[Test]
		public async Task TemplateShouldProcessVariablesInInvertedGroup()
		{
			var data = new Dictionary<string, object>
			{
				{"not_here", false},
				{"placeholder", "a placeholder value"}
			};

			var template = "{{^not_here}}{{../placeholder}}{{/not_here}}";


			var result = await ParserFixture.CreateAndParseWithOptions(template, data, _options);

			Assert.That(result, Is.EqualTo("a placeholder value"));
		}

		[Test]
		public async Task TemplateShouldRenderFalseValue()
		{
			var data = new Dictionary<string, object>
			{
				{"times_won", false}
			};

			var template = "You've won {{times_won}} times!";

			var result = await ParserFixture.CreateAndParseWithOptions(template, data, _options);

			Assert.That(result, Is.EqualTo("You've won False times!"));
		}
	}
}