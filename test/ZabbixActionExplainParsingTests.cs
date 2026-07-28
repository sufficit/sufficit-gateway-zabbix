using System.Text.Json;
using Sufficit.Gateway.Zabbix;

namespace Sufficit.Gateway.Zabbix.Tests;

public sealed class ZabbixActionExplainParsingTests
{
    [Fact]
    public void ParseActionSummaries_ReadsFilterEvaluationTypeAndConditions()
    {
        // Shape of a real action.get response: two conditions of the same type ("trigger name"),
        // combined with evaltype=1 (And) -- the exact configuration that can never fire, because
        // a single event cannot simultaneously equal two different trigger names.
        var json = """
        [
            {
                "actionid": "42",
                "name": "Sufficit Voice Alerts — test",
                "status": "0",
                "filter": {
                    "evaltype": "1",
                    "formula": "A and B",
                    "conditions": [
                        { "conditiontype": "4", "operator": "0", "value": "Incident A" },
                        { "conditiontype": "4", "operator": "0", "value": "Incident B" }
                    ]
                }
            }
        ]
        """;

        using var document = JsonDocument.Parse(json);
        var summaries = ZabbixAutomationService.ParseActionSummaries(document.RootElement);

        var action = Assert.Single(summaries);
        Assert.Equal("42", action.ActionId);
        Assert.Equal("Sufficit Voice Alerts — test", action.Name);
        Assert.Equal(0, action.Status);
        Assert.Equal(1, action.EvaluationType);
        Assert.Contains("never both match one event", action.EvaluationTypeLabel);
        Assert.Equal(2, action.Conditions.Count);
        Assert.Equal("Incident A", action.Conditions[0].Value);
        Assert.Equal("Incident B", action.Conditions[1].Value);
    }

    [Fact]
    public void ParseActionSummaries_ReturnsEmptyListForNonArrayResult()
    {
        using var document = JsonDocument.Parse("{}");
        var summaries = ZabbixAutomationService.ParseActionSummaries(document.RootElement);

        Assert.Empty(summaries);
    }

    [Theory]
    [InlineData(0, "And/Or")]
    [InlineData(1, "And")]
    [InlineData(2, "Or")]
    [InlineData(3, "Custom expression")]
    public void DescribeEvaluationType_CoversEveryDocumentedZabbixValue(int evaluationType, string expectedPrefix)
    {
        var label = ZabbixAutomationService.DescribeEvaluationType(evaluationType);

        Assert.NotNull(label);
        Assert.StartsWith(expectedPrefix, label);
    }

    [Fact]
    public void DescribeEvaluationType_ReturnsNullForUnknownValue()
    {
        Assert.Null(ZabbixAutomationService.DescribeEvaluationType(99));
    }
}
