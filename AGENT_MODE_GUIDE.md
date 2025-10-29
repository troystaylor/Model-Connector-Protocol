# AI Agent Mode Configuration Guide

## Overview

This MCP connector now supports **AI Agent Mode** for Power Automate, allowing makers to use their own AI models (OpenAI, Azure OpenAI, etc.) for orchestration and generation.

## Key Features

✅ **Single Operation** - `InvokeMCP` handles both Agent and MCP modes  
✅ **Dynamic Schema** - Power Automate UI shows relevant parameters with dropdowns  
✅ **4 Execution Modes** - orchestrate, generate, execute, chat  
✅ **Enumerated Choices** - Mode and model selections use dropdown menus  
✅ **Script Configuration** - API keys configured directly in `script.csx`

## Configuration

### 1. Set Your API Key

Open `script.csx` and find the **CONFIGURATION** section (around line 595):

```csharp
// ============================================================================
// CONFIGURATION: Set your AI model credentials here
// ============================================================================
// Option 1: Set API key directly in code (for testing/development)
var apiKey = "YOUR_OPENAI_API_KEY_HERE"; // Replace with your actual key
var baseUrl = "https://api.openai.com/v1"; // Or your Azure OpenAI endpoint

// Option 2: Use environment variables (recommended for production)
// var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
// var baseUrl = Environment.GetEnvironmentVariable("OPENAI_BASE_URL") ?? "https://api.openai.com/v1";

// Option 3: Allow override from context (for multi-tenant scenarios)
if (!string.IsNullOrEmpty(context["apiKey"]?.ToString()))
{
    apiKey = context["apiKey"].ToString();
}
if (!string.IsNullOrEmpty(context["baseUrl"]?.ToString()))
{
    baseUrl = context["baseUrl"].ToString();
}
// ============================================================================
```

**Replace** `"YOUR_OPENAI_API_KEY_HERE"` with your actual OpenAI API key.

### 2. (Optional) Configure for Azure OpenAI

For Azure OpenAI, update the `baseUrl`:

```csharp
var apiKey = "your-azure-openai-key";
var baseUrl = "https://your-resource.openai.azure.com/openai/deployments/your-deployment";
```

And set the model in DEFAULT_MODEL constant at the top of the file.

## Usage in Power Automate

### Dynamic Schema

When you add the connector action in Power Automate, you'll see:

**Required Field:**
- **Input** - Your natural language request

**Optional Fields (with dropdowns):**
- **Execution Mode** - Choose from:
  - `orchestrate` - AI plans and auto-executes tools
  - `generate` - Pure AI generation with context
  - `execute` - Direct tool execution
  - `chat` - Conversational with memory
  - `mcp` - Traditional MCP JSON-RPC mode

- **Model** (in Options) - Choose from:
  - `gpt-4-turbo`
  - `gpt-4`
  - `gpt-3.5-turbo`
  - `gpt-4o`
  - `gpt-4o-mini`
  - *(empty to use configured default)*

### Example 1: Simple Orchestration

```json
{
  "mode": "orchestrate",
  "input": "What's the weather in Seattle and should I bring an umbrella?"
}
```

The AI will:
1. Call `get_weather` tool
2. Analyze the results
3. Return: "It's 52°F and rainy. Yes, bring an umbrella!"

### Example 2: Conversational Chat

```json
{
  "mode": "chat",
  "input": "Compare weather in NYC and LA",
  "context": {
    "conversationHistory": []
  },
  "options": {
    "model": "gpt-4-turbo",
    "temperature": 0.7
  }
}
```

### Example 3: Direct Tool Execution

```json
{
  "mode": "execute",
  "input": "Get weather for Tokyo",
  "options": {
    "autoExecuteTools": true
  }
}
```

### Example 4: Traditional MCP Mode

```json
{
  "jsonrpc": "2.0",
  "method": "tools/list",
  "id": 1
}
```

## Response Structure

### Agent Mode Response:

```json
{
  "response": "Natural language answer from AI",
  "mode": "orchestrate",
  "execution": {
    "plan": "Steps taken by AI",
    "toolCalls": [
      {
        "tool": "get_weather",
        "arguments": {"location": "Seattle"},
        "result": {...},
        "success": true
      }
    ],
    "toolsExecuted": 1
  },
  "metadata": {
    "tokensUsed": 1245,
    "estimatedCost": "$0.023",
    "duration": "2.3s",
    "model": "gpt-4-turbo"
  },
  "conversationHistory": [...],
  "error": null
}
```

### MCP Mode Response:

```json
{
  "jsonrpc": "2.0",
  "result": {
    "tools": [...]
  },
  "id": 1
}
```

## Advanced Options

### Context Parameters:
- `conversationHistory` - Array of previous messages (chat mode)
- `systemPrompt` - Custom AI instructions
- `availableTools` - Limit which tools AI can use
- `resources` - Resource URIs to include as context

### Options Parameters:
- `autoExecuteTools` - Allow AI to call tools automatically (default: true)
- `maxToolCalls` - Safety limit on tool calls (default: 10)
- `includeToolResults` - Return detailed tool results (default: true)
- `temperature` - AI creativity 0.0-2.0 (default: 0.7)
- `maxTokens` - Max response length (default: 1000)
- `model` - Override configured model

## Multi-Tenant Scenarios

For connectors serving multiple tenants, you can pass API keys per request:

```json
{
  "mode": "orchestrate",
  "input": "Your request",
  "context": {
    "apiKey": "tenant-specific-key",
    "baseUrl": "https://tenant-openai-endpoint.com/v1"
  }
}
```

This overrides the script configuration for that specific request.

## Security Best Practices

1. **Never commit API keys** to source control
2. **Use environment variables** in production
3. **Rotate keys regularly**
4. **Monitor usage and costs** through OpenAI dashboard
5. **Set appropriate maxToolCalls** limits to prevent runaway costs
6. **Use connection-level authentication** when possible

## Troubleshooting

### "API key not configured" Error
- Check line ~595 in `script.csx`
- Ensure you replaced `YOUR_OPENAI_API_KEY_HERE`
- Or provide API key in `context.apiKey`

### "OpenAI API error: 401"
- Invalid API key
- Key may be expired or revoked
- Check OpenAI dashboard

### "OpenAI API error: 429"
- Rate limit exceeded
- Reduce `maxToolCalls` or `temperature`
- Upgrade OpenAI plan

### No response or timeout
- Increase `maxTokens`
- Check if tools are executing properly
- Review logs in Power Automate run history

## Cost Management

Monitor costs with the returned metadata:

```json
{
  "metadata": {
    "tokensUsed": 1245,
    "estimatedCost": "$0.023",
    "model": "gpt-4-turbo"
  }
}
```

Cost estimates are approximate based on:
- GPT-4: ~$0.03 per 1K tokens
- GPT-3.5: ~$0.002 per 1K tokens
- GPT-4o: Varies by model version

## Files Modified

1. **apiDefinition.swagger.json** - Added dynamic schema operation
2. **script.csx** - Added:
   - Request router (MCP vs Agent detection)
   - HandleAgentRequest with 4 modes
   - OpenAI API integration
   - Function calling loop
   - HandleGetSchema for dynamic UI
   - Configuration section for API keys

## Next Steps

1. Set your API key in `script.csx`
2. Deploy connector to Power Platform
3. Create a test flow in Power Automate
4. Try different modes and see AI orchestration in action!

## Support

For issues or questions:
- Check logs in Power Automate run history
- Review OpenAI API status: https://status.openai.com
- Consult MCP documentation: https://modelcontextprotocol.io
