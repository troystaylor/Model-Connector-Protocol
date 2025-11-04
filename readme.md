# Model Connector Protocol for Power Automate

## Overview

This repository contains two production-ready Power Automate custom connectors built using the Model Connector Protocol (MCP) that integrate AI models with specialized capabilities:

### 1. [Generic MCP Connector](./Generic-MCP.md)
Build AI agents with custom tools using Model Context Protocol (MCP). Define your own tools, resources, and prompts for intelligent automation.

**Use Cases:**
- Business process automation with custom APIs
- Multi-system orchestration
- Intelligent routing and decision making
- Data enrichment and analysis

### 2. [MS Learn MCP Connector](./MS%20Learn%20ModelConnectorProtocol/)
AI assistant powered by Microsoft Learn documentation. Ask questions about Microsoft technologies and get answers grounded in official documentation with citations.

**Use Cases:**
- Developer assistance and training
- Technical support automation
- Documentation search and summarization
- Code sample discovery

## Quick Comparison

| Feature | Generic MCP Connector | MS Learn MCP Connector |
|---------|----------------------|------------------------|
| **Purpose** | Custom tool orchestration | Microsoft Learn documentation |
| **Tools** | You define them | Pre-built (docs search, code samples, fetch) |
| **Data Source** | Your APIs/systems | Microsoft Learn (learn.microsoft.com) |
| **Customization** | Fully customizable | Fixed tools, customizable AI behavior |
| **Best For** | Building unique AI agents | Microsoft tech questions & docs |

## Repository Structure

```
Model Connector Protocol/
├── README.md                           # This file - repository overview
├── Generic-MCP-Connector.md            # Generic MCP Connector documentation
├── script.csx                          # Generic MCP Connector code
├── apiDefinition.swagger.json         # Generic connector API definition
├── apiProperties.json                 # Generic connector properties
├── Alternate Authentication Types/    # Auth examples (API key, OAuth, etc.)
└── MS Learn ModelConnectorProtocol/   # MS Learn MCP Connector (complete)
    ├── readme.md                      # MS Learn connector documentation
    ├── script.csx                     # MS Learn connector code
    ├── apiDefinition.swagger.json     # MS Learn API definition
    └── apiProperties.json             # MS Learn connector properties
```

## Prerequisites

- Power Platform environment with Premium licensing
- Power Platform CLI ([installation guide](https://learn.microsoft.com/power-platform/developer/cli/introduction))
- AI model API key:
  - **Anthropic Claude** (recommended) - Get key from [Anthropic Console](https://console.anthropic.com/)
  - **OpenAI** - Get key from [OpenAI Platform](https://platform.openai.com/)
  - **Azure OpenAI** - Deploy in [Azure Portal](https://portal.azure.com)

## Getting Started

### Choose Your Connector

**For Custom AI Agents:**
1. Use the **Generic MCP Connector** (root folder)
2. Follow the [Generic MCP Connector Guide](./Generic-MCP-Connector.md)
3. Define your custom tools in `script.csx`

**For Microsoft Learn Documentation:**
1. Use the **MS Learn MCP Connector** (`MS Learn ModelConnectorProtocol/` folder)
2. Follow the [MS Learn Connector Guide](./MS%20Learn%20ModelConnectorProtocol/readme.md)
3. Configure Azure OpenAI or other AI provider

### Deployment Steps (Both Connectors)

1. **Authenticate with Power Platform CLI:**
   ```powershell
   pac auth create --environment https://yourorg.crm.dynamics.com
   ```

2. **Navigate to connector folder:**
   ```powershell
   # For Generic MCP:
   cd "Model Connector Protocol"
   
   # For MS Learn MCP:
   cd "Model Connector Protocol/MS Learn ModelConnectorProtocol"
   ```

3. **Create the connector:**
   ```powershell
   pac connector create --settings-file apiProperties.json
   ```

4. **Create connection in Power Automate:**
   - Go to Power Automate → Data → Connections
   - Create new connection
   - Enter your AI model API key

5. **Test in a flow:**
   - Create a test flow
   - Add connector action
   - Send a natural language request

## Configuration

Both connectors support:
- **Multiple AI Providers**: Anthropic Claude, OpenAI, Azure OpenAI
- **Customizable System Instructions**: Control AI behavior and personality
- **Configurable Parameters**: Temperature, token limits, tool execution settings
- **Custom Code**: Full C# scripting for advanced scenarios

### Provider Configuration (script.csx)

Update these constants in your chosen connector's `script.csx`:

```csharp
// For Anthropic Claude (default)
private const string DEFAULT_BASE_URL = "https://api.anthropic.com/v1";
private const string DEFAULT_MODEL = "claude-sonnet-4-20250514";

// For OpenAI
private const string DEFAULT_BASE_URL = "https://api.openai.com/v1";
private const string DEFAULT_MODEL = "gpt-4o";

// For Azure OpenAI
private const string DEFAULT_BASE_URL = "https://YOUR_RESOURCE_NAME.openai.azure.com";
private const string DEFAULT_MODEL = "gpt-4o";
```

## Features

### Generic MCP Connector
- ✅ Define custom tools (APIs, databases, web scraping, etc.)
- ✅ Resource management (HTTP/HTTPS data sources)
- ✅ Reusable prompts library
- ✅ Automatic tool selection and chaining
- ✅ Multi-step reasoning
- ✅ Full Model Context Protocol support

### MS Learn MCP Connector
- ✅ Search Microsoft Learn documentation
- ✅ Search code samples with language filtering
- ✅ Fetch complete documentation pages
- ✅ Automatic source citations with URLs and titles
- ✅ AI synthesis of multiple sources
- ✅ Markdown-formatted responses

## Examples

### Generic MCP Connector Example
```
Input: "Check the weather in Seattle and if it's raining, send a Slack message to #team"

AI Agent:
1. Calls WeatherAPI tool for Seattle
2. Analyzes result (raining = true)
3. Calls SlackAPI tool to send message
4. Returns: "It's currently raining in Seattle. I've notified #team channel."
```

### MS Learn MCP Connector Example
```
Input: "How do I create a custom connector with OAuth authentication?"

AI Assistant:
1. Searches Microsoft Learn docs for custom connectors + OAuth
2. Fetches relevant pages
3. Synthesizes answer with code samples
4. Returns: Markdown response with step-by-step guide and 5+ citation links
```

## Cost Considerations

**AI Model Usage:**
- You pay your AI provider directly based on tokens used
- Typical costs (per 1M tokens):
  - Claude 3.5 Sonnet: $3 input / $15 output
  - GPT-4o: $2.50 input / $10 output
  - Azure OpenAI: Similar, plus Azure hosting

**Power Platform:**
- Custom connectors require Premium licensing
- No additional per-request costs from Microsoft

**External APIs (Generic MCP only):**
- Your custom tools may call paid APIs
- Costs vary by service

## Security Best Practices

- ✅ Store API keys in Power Platform connections (secure storage)
- ✅ Use HTTPS for all external calls
- ✅ Validate all tool inputs
- ✅ Implement rate limiting in custom tools
- ✅ Log tool executions for audit trails
- ❌ Never hardcode API keys in connector code
- ❌ Don't expose sensitive data in tool responses
- ❌ Avoid unrestricted access to dangerous operations

## Troubleshooting

### "API key not configured"
- Ensure you entered the API key when creating the connection
- Verify the key is valid in your AI provider's console
- Delete and recreate the connection

### "Rate limit exceeded"
- Wait for the specified retry time
- Request higher quota from your AI provider
- Use a model with higher rate limits

### "Tool execution failed" (Generic MCP)
- Check tool implementation in `script.csx`
- Verify external APIs are accessible
- Review Power Platform connector logs

### "Host resolution error"
- Ensure custom code connectors use a valid host
- Both connectors use dummy hosts (e.g., `api.example.com`)
- Custom code handles all requests internally

## Support & Contribution

### Getting Help
- Review connector-specific READMEs for detailed guides
- Check [Power Platform Community](https://powerusers.microsoft.com/)
- Review [Custom Connectors Documentation](https://learn.microsoft.com/connectors/custom-connectors/)

### Contributing
Contributions welcome! Please:
1. Fork the repository
2. Create a feature branch
3. Test your changes thoroughly
4. Submit a pull request with clear description

### Reporting Issues
Include:
- Which connector (Generic MCP or MS Learn MCP)
- AI provider being used
- Sample request (sanitized)
- Error message and logs
- Steps to reproduce

## Resources

### Model Context Protocol
- [MCP Documentation](https://modelcontextprotocol.io/)
- [MCP Specification](https://modelcontextprotocol.io/specification/latest)
- [MCP GitHub](https://github.com/modelcontextprotocol)

### AI Providers
- [Anthropic Claude](https://www.anthropic.com/claude)
- [OpenAI Platform](https://platform.openai.com/)
- [Azure OpenAI Service](https://azure.microsoft.com/products/ai-services/openai-service)

### Power Platform
- [Custom Connectors Overview](https://learn.microsoft.com/connectors/custom-connectors/)
- [Custom Connector Code](https://learn.microsoft.com/connectors/custom-connectors/write-code)
- [Power Platform CLI](https://learn.microsoft.com/power-platform/developer/cli/introduction)
- [Power Automate](https://learn.microsoft.com/power-automate/)

### Microsoft Learn
- [Microsoft Learn MCP Server](https://learn.microsoft.com/api/mcp)
- [Microsoft Learn](https://learn.microsoft.com/)

## License

MIT License - See individual connector folders for full license text.

## Author

Troy Taylor
- Email: troy@troystaylor.com
- Publisher: Troy Taylor

---

**Ready to get started?**
- Build custom AI agents → [Generic MCP Connector Guide](./Generic-MCP-Connector.md)
- Search Microsoft Learn docs → [MS Learn MCP Connector Guide](./MS%20Learn%20ModelConnectorProtocol/readme.md)
