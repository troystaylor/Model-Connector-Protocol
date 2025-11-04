# Model Connector Protocol for AI Agent in Power Automate

## Overview

This connector brings AI-powered automation to Power Automate with intelligent tool selection, generative decision-making, and automated summarization. It combines the Model Context Protocol (MCP) for tool orchestration with direct AI model integration for generative capabilities.

## Key Features

### 🤖 AI Agent Automation
Send natural language requests and let the AI agent automatically:
- **Select and execute tools** based on your request
- **Make intelligent decisions** about which actions to take
- **Generate summaries** of results in natural language
- **Handle multi-step workflows** with automatic tool chaining

### 🛠️ Powered by Model Context Protocol
Define custom capabilities for your AI agent:
- **Tools**: Custom operations you define (API calls, data processing, etc.)
- **Resources**: HTTP/HTTPS data sources for the AI to read
- **Prompts**: Reusable templates for common AI tasks

### 🔑 Your AI Model, Your Control
- Bring your own API key (Anthropic Claude or OpenAI)
- Configure once in the connector - no model switching needed per request
- Customize AI behavior through system instructions
- Control temperature, token limits, and tool execution

## How It Works

1. **Define Your Tools** in the connector (weather API, database queries, web scraping, etc.)
2. **Configure Your AI Model** (provide API key when creating connection)
3. **Send Natural Language Requests** from Power Automate
4. **AI Agent Automatically**:
   - Understands your request
   - Selects appropriate tools
   - Executes them in the right order
   - Generates a natural language summary
5. **Use Results** in your automation workflow

## Quick Start

1. **Import the connector** to Power Platform
2. **Create a connection** with your AI model API key (Anthropic or OpenAI)
3. **Define your tools** in `script.csx` (edit `GetDefinedTools()` method)
4. **Use in Power Automate** by sending natural language requests



## Configuration Options

### AI Model Settings

**Model Configuration:**
The model is configured once in the connector's `script.csx` file (DEFAULT_MODEL constant).

- **Anthropic Claude** (default: `claude-sonnet-4-20250514`):
  - `claude-sonnet-4-20250514` - Best balance of speed and intelligence
  - `claude-opus-4-20250514` - Highest intelligence, slower
  - `claude-haiku-4-20250514` - Fastest, most economical
  
- **OpenAI**:
  - `gpt-4o` - Latest GPT-4 optimized
  - `gpt-4-turbo` - Fast GPT-4
  - `gpt-4` - Standard GPT-4
  - `gpt-3.5-turbo` - GPT-3.5 (faster, more economical)

*Note: To use a different model, update the DEFAULT_MODEL constant in script.csx. All connections will use the configured model.*

**Parameters:**
- `temperature` (0.0-1.0): Controls creativity vs. consistency (default: 0.7)
- `maxTokens`: Maximum response length (default: 4096)
- `autoExecuteTools`: Let AI automatically run tools (default: true)
- `maxToolCalls`: Limit number of tool executions (default: 10)
- `includeToolResults`: Include tool details in response (default: true)

### System Instructions

Customize AI behavior in `script.csx` by editing the `GetSystemInstructions()` method. Define personality, business rules, and response format.

## Use Cases

### Business Process Automation
- **Intelligent routing**: "Route this support ticket to the right team"
- **Approval workflows**: "Should we approve this expense based on policy?"
- **Data enrichment**: "Look up customer details and update the record"

### Data Analysis & Reporting
- **Smart queries**: "What were our top products last quarter?"
- **Anomaly detection**: "Are there any unusual patterns in today's sales?"
- **Summarization**: "Summarize the key points from this week's metrics"

### Integration & Orchestration
- **Multi-system workflows**: "Check inventory, create PO if needed, notify procurement"
- **Conditional logic**: "If revenue is down, send alert; otherwise, generate standard report"
- **Error handling**: "If the API fails, try the backup source and log the issue"

## Architecture

This connector uses AI Agent mode for intelligent automation:

```
Power Automate → AI Agent Connector
                      ↓
                AI Model API (Claude/GPT)
                      ↓
            [Tool Selection & Execution]
                      ↓
            Your Custom Tools (APIs, DBs, etc.)
                      ↓
            Natural Language Summary
```

**Key Benefits:**
- Natural language input/output
- Automatic tool selection
- Multi-step reasoning
- Intelligent error handling
- No need to know tool names or parameters

*Note: The connector uses Model Context Protocol (MCP) internally for tool orchestration, but this is transparent to users.*

## Deployment & Configuration

### Prerequisites
- Power Platform environment with Premium licensing
- AI model API key (Anthropic Claude or OpenAI)
- Tools/APIs you want to expose to the AI agent

### Installation Steps

1. **Install Power Platform CLI**
   - Download from [Microsoft Learn](https://learn.microsoft.com/power-platform/developer/cli/introduction)
   - Or install via: `dotnet tool install --global Microsoft.PowerApps.CLI.Tool`

2. **Update Publisher Information**
   - Edit apiProperties.json with your contact details
   - Update connector name and description

3. **Configure AI Model Settings**
   - Set your model in script.csx (DEFAULT_MODEL constant)
   - Set your provider's base URL (DEFAULT_BASE_URL constant)
     * Anthropic: `https://api.anthropic.com/v1`
     * OpenAI: `https://api.openai.com/v1`
   - Customize system instructions in GetSystemInstructions()
   - Adjust default temperature, token limits as needed

4. **Define Your Tools**
   - Add tool definitions in GetDefinedTools()
   - Implement tool handlers (Execute{ToolName}Tool methods)
   - Add resources in GetDefinedResources() if needed
   - Add prompts in GetDefinedPrompts() if needed

5. **Deploy Using Power Platform CLI**
   ```bash
   pac auth create --environment <your-environment-url>
   pac connector create --settings-file apiProperties.json
   ```

6. **Create Connection**
   - Go to Power Automate → Data → Connections
   - Create new connection with this connector
   - Enter your AI model API key

7. **Test**
   - Create a test flow
   - Call the connector with a simple natural language request
   - Verify AI agent responds appropriately

## Error Handling

The connector returns structured error responses with JSON-RPC 2.0 error codes.

**Standard Error Codes:**
- `-32700`: Parse error (invalid JSON)
- `-32600`: Invalid request format
- `-32601`: Method/tool not found
- `-32602`: Invalid parameters
- `-32603`: Internal error
- `-32000`: Tool execution error

## Best Practices

### Tool Design
- ✅ Keep tools focused on single responsibility
- ✅ Provide clear, descriptive tool names
- ✅ Write detailed descriptions for AI understanding
- ✅ Validate all input parameters
- ✅ Return structured, consistent data
- ❌ Don't create overly complex tools
- ❌ Don't include sensitive credentials in tool descriptions

### AI Instructions
- ✅ Be specific about desired behavior
- ✅ Include business rules and constraints
- ✅ Specify response format preferences
- ✅ Add examples of good responses
- ❌ Don't make instructions too restrictive
- ❌ Don't include secrets or keys

### Performance
- ✅ Set appropriate maxTokens limits
- ✅ Use lower temperature for consistent outputs
- ✅ Limit maxToolCalls for complex requests
- ✅ Cache frequently used data in tools
- ❌ Don't set temperature too high (>0.9) for business logic
- ❌ Don't allow unlimited tool chaining

### Security
- ✅ Store API keys in connection parameters
- ✅ Validate all tool inputs
- ✅ Use HTTPS for all external calls
- ✅ Log tool executions for audit
- ❌ Don't expose sensitive data in tool responses
- ❌ Don't allow unrestricted access to dangerous operations

## Frequently Asked Questions

### What AI models are supported?
Anthropic Claude (recommended default) and OpenAI GPT-4 models. Each connector is configured for one model - set the DEFAULT_MODEL constant in script.csx to your chosen model. You provide your own API key for that model's provider.

### Do I need to know the Model Context Protocol?
No. Simply send natural language requests - the AI handles tool selection automatically. MCP is used internally for tool orchestration but requires no knowledge from users.

### How much does it cost?
You pay only for:
- Your AI model API usage (based on tokens used per request)
- Your external API calls (tools you define)
- No additional Power Automate cost

### Is my data secure?
- Your API keys are stored securely in Power Platform connections
- AI Requests go directly to your chosen AI provider
- MCP server data stays in your environment
- No data is stored by this connector

### Can I customize the AI behavior?
Yes! Edit the system instructions in script.csx to control:
- Personality and tone
- Business rules and constraints
- Response format
- Decision-making logic

### What's the difference from Copilot Studio?
Copilot Studio is for conversational agents. This connector is for:
- Power Automate flow automation
- Semi-deterministic, AI-guided responses
- Custom tool orchestration
- Backend process intelligence

### Can I switch AI providers?
Yes, but it requires updating the connector configuration:
1. Update the DEFAULT_MODEL constant in script.csx
2. Update the DEFAULT_BASE_URL if switching between Anthropic and OpenAI
3. Create a new connection with the appropriate provider's API key

Each connector instance is configured for one model/provider.

## Troubleshooting

### "API key not configured"
- Verify you entered the AI Model API Key when creating the connection
- Check the key is valid and active in your AI provider account
- Try creating a new connection

### "Tool execution failed"
- Check tool implementation logs in Power Platform
- Verify tool handler matches tool name in GetDefinedTools()
- Ensure external APIs are accessible
- Validate tool arguments match schema

### "Max tool calls reached"
- Increase `maxToolCalls` option (default: 10)
- Simplify the request to require fewer steps
- Check for circular tool calling logic

### "Token limit exceeded"
- Reduce `maxTokens` option
- Make tool responses more concise
- Use fewer tools in complex workflows
- Consider using a model with larger context window

### AI selecting wrong tools
- Improve tool descriptions with clear use cases
- Add more specific system instructions
- Use more descriptive tool names
- Test with lower temperature for consistency

## References & Resources

### Model Context Protocol
- [MCP Documentation](https://modelcontextprotocol.io/)
- [MCP Specification](https://modelcontextprotocol.io/specification/latest)
- [MCP GitHub](https://github.com/modelcontextprotocol)

### AI Providers
- [Anthropic Claude](https://www.anthropic.com/claude)
- [OpenAI Platform](https://platform.openai.com/)

### Power Platform
- [Custom Connectors Documentation](https://learn.microsoft.com/en-us/connectors/custom-connectors/)
- [Custom Connector Code](https://learn.microsoft.com/en-us/connectors/custom-connectors/write-code)
- [Power Automate Documentation](https://learn.microsoft.com/en-us/power-automate/)

## Support & Contribution

### Getting Help
- Review this readme and connector documentation
- Check MCP specification for protocol questions
- Consult AI provider documentation for model-specific issues
- Contact connector publisher for template-specific questions

### Reporting Issues
When reporting issues, include:
- Connector version
- AI model being used
- Sample request (sanitized)
- Error message and logs
- Steps to reproduce

## License

MIT License

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
