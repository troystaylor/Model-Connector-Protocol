# MS Learn MConnectorP

## Overview

This connector brings Microsoft Learn documentation directly into Power Automate through an AI-powered assistant with Model Context Protocol implemented in custom code. Ask questions about Microsoft technologies and get accurate answers grounded in official Microsoft Learn documentation, complete with code samples and citations.

## Key Features

### 📚 Microsoft Learn Integration
Access official Microsoft documentation through AI:
- **Search documentation** across all Microsoft Learn content
- **Retrieve code samples** with optional language filtering (C#, Python, JavaScript, etc.)
- **Fetch complete pages** for detailed tutorials and examples
- **Automatic citations** with URLs and page titles for every source

### 🤖 AI-Powered Understanding
Powered by Azure OpenAI (GPT-4o):
- **Natural language queries** - ask questions in plain English
- **Intelligent synthesis** - combines information from multiple sources
- **Context-aware responses** - understands Microsoft technology context
- **Code-focused** - prioritizes practical implementation examples

### 🔧 Built on MCP
Leverages the Microsoft Learn Model Context Protocol (MCP) Server:
- **Three specialized tools**:
  - `microsoft_docs_search` - Search documentation
  - `microsoft_code_sample_search` - Find code examples
  - `microsoft_docs_fetch` - Get complete pages
- **Always up-to-date** - queries live Microsoft Learn content
- **Comprehensive coverage** - Azure, Power Platform, .NET, Microsoft 365, and more

## How It Works

1. **Ask a Question** in natural language from Power Automate
   - "How do I create a custom connector?"
   - "Show me C# code for Azure OpenAI chat completions"
   - "What are Power Platform connectors?"

2. **AI Agent Automatically**:
   - Searches Microsoft Learn documentation
   - Finds relevant code samples when needed
   - Fetches complete pages for detailed information
   - Synthesizes information from multiple sources

3. **Get Comprehensive Answers** with:
   - Natural language explanations
   - Code examples with proper syntax
   - Direct links to source documentation
   - Page titles and URLs for all citations

4. **Use in Workflows**:
   - Automated documentation lookups
   - Context for AI-powered assistants
   - Training material generation
   - Technical support automation

## Quick Start

1. **Import the connector** to Power Platform using PAC CLI
2. **Create a connection** with your Azure OpenAI API key
3. **Use in Power Automate** - just ask questions about Microsoft technologies!

### Example Requests

```
"How do I authenticate with Azure OpenAI?"
"Show me Python code for calling the Microsoft Graph API"
"What's the difference between Power Apps canvas and model-driven apps?"
"How do I deploy an Azure Function?"
```



## Configuration Options

### AI Model Settings

**Configured Model:**
- **Azure OpenAI GPT-4o** (hardcoded in `script.csx`)
- Endpoint: Your Azure OpenAI resource URL
- API Key: Provided when creating connection

**Optional Parameters:**
- `temperature` (0.0-1.0): Controls creativity vs. consistency (default: 0.7)
- `maxTokens`: Maximum response length (default: 16000)
- `maxToolCalls`: Limit number of tool executions (default: 10)

### System Instructions

The connector is pre-configured with instructions optimized for Microsoft Learn documentation assistance. The AI is instructed to:
- Search Microsoft Learn when users ask about Microsoft technologies
- Use code sample search for implementation examples
- Fetch complete pages when detailed information is needed
- Always cite sources with URLs
- Provide clear, technically accurate responses with code examples

## Use Cases

### Developer Assistance
- **Quick documentation lookups**: "How do I use Azure Key Vault in .NET?"
- **Code examples**: "Show me JavaScript code for Power Automate custom connectors"
- **API references**: "What parameters does the Microsoft Graph users API accept?"

### Training & Onboarding
- **Learning paths**: "What should I learn to build Power Platform solutions?"
- **Best practices**: "What are the security best practices for Azure Functions?"
- **Technology comparisons**: "When should I use Azure App Service vs Azure Functions?"

### Technical Support Automation
- **Automated responses**: Build flows that answer common technical questions
- **Knowledge base**: Integrate into chatbots or virtual agents
- **Contextual help**: Provide in-app guidance based on user questions

### Content Generation
- **Documentation summaries**: Generate overviews of Microsoft technologies
- **Tutorial creation**: Extract step-by-step guides from Learn content
- **Code snippets**: Find and adapt official code samples for your needs

## Architecture

This connector integrates three systems:

```
Power Automate → MS Learn AI Connector
                      ↓
            Azure OpenAI (GPT-4o)
                      ↓
        [Tool Selection & Execution]
                      ↓
     Microsoft Learn MCP Server
     (https://learn.microsoft.com/api/mcp)
                      ↓
        - microsoft_docs_search
        - microsoft_code_sample_search  
        - microsoft_docs_fetch
                      ↓
       Microsoft Learn Content
```

**Key Components:**
- **Azure OpenAI**: Understands queries and orchestrates tool calls
- **Learn MCP Server**: Public Microsoft service providing documentation access
- **Custom Code**: Handles MCP protocol and formats responses with proper citations

**Data Flow:**
1. User asks question in Power Automate
2. Azure OpenAI decides which Learn MCP tools to use
3. Connector calls Learn MCP Server (no authentication required)
4. Learn MCP returns documentation/code samples
5. Azure OpenAI synthesizes answer with citations
6. Response includes URLs, titles, and source attribution

## Deployment & Configuration

### Prerequisites
- Power Platform environment with Premium licensing
- Azure OpenAI resource with GPT-4o model deployed
- Azure OpenAI API key

### Installation Steps

1. **Install Power Platform CLI**
   - Download from [Microsoft Learn](https://learn.microsoft.com/power-platform/developer/cli/introduction)
   - Or install via: `dotnet tool install --global Microsoft.PowerApps.CLI.Tool`

2. **Configure Azure OpenAI Settings**
   - Edit `script.csx`
   - Set `DEFAULT_BASE_URL` to your Azure OpenAI endpoint (e.g., `https://your-resource.openai.azure.com`)
   - Set `DEFAULT_MODEL` to `gpt-4o` (or your deployed model name)

3. **Update Publisher Information**
   - Edit `apiProperties.json` with your contact details
   - Update connector name and description if desired

4. **Deploy Using Power Platform CLI**
   ```bash
   pac auth create --environment <your-environment-url>
   pac connector create --api-definition-file apiDefinition.swagger.json --api-properties-file apiProperties.json --script-file script.csx
   ```

5. **Create Connection**
   - Go to Power Automate → Data → Connections
   - Create new connection with this connector
   - Enter your Azure OpenAI API key

6. **Test**
   - Create a test flow
   - Call the connector: "Tell me about Power Platform connectors"
   - Verify response includes documentation with URLs

## Error Handling

The connector returns structured error responses with detailed information.

**Common Scenarios:**
- **No documentation found**: Returns empty results with message
- **Learn MCP server errors**: Provides error details from the MCP service
- **Azure OpenAI errors**: Returns API error messages (rate limits, authentication, etc.)
- **Invalid queries**: Validates and returns parameter errors

**Response Structure:**
All responses include:
- `response`: Natural language answer
- `execution`: Tool calls and results
- `metadata`: Duration, model used, token count
- `error`: Error message if request failed

## Best Practices

### Query Design
- ✅ Ask specific questions about Microsoft technologies
- ✅ Request code examples by language ("Show me C# code for...")
- ✅ Ask for comparisons ("What's the difference between...")
- ✅ Request step-by-step guidance
- ❌ Don't ask about non-Microsoft technologies
- ❌ Don't expect real-time or proprietary information

### Response Usage
- ✅ Use source URLs for users to learn more
- ✅ Extract code samples for implementation
- ✅ Cache responses for frequently asked questions
- ✅ Log queries for analytics and improvement
- ❌ Don't assume responses are always current (check docs)
- ❌ Don't use as a substitute for security testing

### Performance
- ✅ Set appropriate maxTokens for your needs
- ✅ Use lower temperature (0.3-0.5) for factual queries
- ✅ Limit tool calls to avoid excessive Learn MCP requests
- ❌ Don't set temperature too high (reduces accuracy)
- ❌ Don't make unnecessarily complex queries

### Security
- ✅ Store Azure OpenAI key securely in connection
- ✅ Limit connector access to authorized users
- ✅ Monitor usage and costs
- ❌ Don't expose API keys in flows or apps
- ❌ Don't use for sensitive/confidential queries

## Frequently Asked Questions

### What topics does this cover?
All publicly available Microsoft Learn documentation including:
- Azure (all services)
- Power Platform (Power Apps, Power Automate, Power BI, Copilot Studio)
- Microsoft 365 (SharePoint, Teams, Graph API, etc.)
- .NET and development tools
- Dynamics 365
- Security and compliance
- And more...

### Does it include training modules and certification content?
Currently, the Learn MCP Server provides access to documentation articles only. Training modules, learning paths, and certification exam content are not included. Use the [Learn Catalog API](https://learn.microsoft.com/en-us/training/support/catalog-api) for that content.

### How current is the information?
The Microsoft Learn MCP Server refreshes:
- Incrementally with each content update
- Completely once per day

The AI may have training data that's older, but the Learn MCP tools provide access to current published documentation.

### What AI model is used?
Azure OpenAI GPT-4o, configured in the connector. You provide your own Azure OpenAI resource and API key.

### How much does it cost?
- **Learn MCP Server**: Free (no authentication required)
- **Azure OpenAI**: Pay per token (see [Azure OpenAI pricing](https://azure.microsoft.com/pricing/details/cognitive-services/openai-service/))
- **Power Platform**: Premium connector (requires Premium license)

Typical query costs 5,000-15,000 tokens (~$0.05-$0.15 with GPT-4o pricing).

### Can I customize the AI behavior?
Yes, edit the system instructions in `script.csx` (GetSystemInstructions method) to control:
- Response style and format
- When to search vs. fetch vs. code sample search
- Citation preferences
- Level of detail

### Is my data secure?
- API keys stored securely in Power Platform connections
- Queries go to Azure OpenAI (your tenant) and Learn MCP (public Microsoft service)
- No data is stored by this connector
- Learn MCP is a public service - don't send confidential queries

### Can I use a different AI model?
Yes, but requires code changes:
1. Update `DEFAULT_BASE_URL` and `DEFAULT_MODEL` in script.csx
2. For non-Azure providers (OpenAI, Anthropic), update authentication headers
3. Test thoroughly as different models may select tools differently

### Does this work offline?
No, it requires:
- Internet connectivity
- Access to Azure OpenAI service
- Access to learn.microsoft.com/api/mcp

## Troubleshooting

### "API key not configured"
- Verify you entered the Azure OpenAI API Key when creating the connection
- Check the key is valid in your Azure OpenAI resource
- Ensure the key has access to the GPT-4o deployment
- Try creating a new connection

### "Learn MCP server errors"
- Check https://learn.microsoft.com/api/mcp is accessible
- Verify your environment has internet connectivity
- Check Power Platform isn't blocking the Learn MCP endpoint
- Wait and retry if service is temporarily unavailable

### "No results found"
- Try different search terms or phrasing
- Be more specific about the Microsoft technology
- Use official Microsoft product names
- Try broader queries first, then narrow down

### "Token limit exceeded"
- Reduce `maxTokens` option
- Ask more focused questions
- Avoid requesting too many code samples at once
- Consider breaking complex queries into multiple requests

### AI not finding relevant documentation
- Use specific Microsoft product/service names
- Include version numbers if relevant ("Azure Functions v4")
- Try multiple phrasings of the same question
- Request specific documentation sections

### Slow responses
- Complex queries with multiple tool calls take longer
- Learn MCP Server response time varies
- Azure OpenAI processing time depends on token count
- Consider caching common queries

## References & Resources

### Microsoft Learn MCP Server
- [MCP Server Overview](https://learn.microsoft.com/en-us/training/support/mcp)
- [Developer Reference](https://learn.microsoft.com/en-us/training/support/mcp-developer-reference)
- [Best Practices](https://learn.microsoft.com/en-us/training/support/mcp-best-practices)
- [Release Notes](https://learn.microsoft.com/en-us/training/support/mcp-release-notes)
- [GitHub Repository](https://github.com/microsoftdocs/mcp)

### Model Context Protocol
- [MCP Documentation](https://modelcontextprotocol.io/)
- [MCP Specification](https://spec.modelcontextprotocol.io/)
- [MCP GitHub](https://github.com/modelcontextprotocol)

### Azure OpenAI
- [Azure OpenAI Service](https://azure.microsoft.com/products/ai-services/openai-service)
- [GPT-4o Documentation](https://learn.microsoft.com/en-us/azure/ai-services/openai/concepts/models#gpt-4o-and-gpt-4-turbo)
- [Pricing](https://azure.microsoft.com/pricing/details/cognitive-services/openai-service/)

### Power Platform
- [Custom Connectors Documentation](https://learn.microsoft.com/en-us/connectors/custom-connectors/)
- [Custom Connector Code](https://learn.microsoft.com/en-us/connectors/custom-connectors/write-code)
- [Power Automate Documentation](https://learn.microsoft.com/en-us/power-automate/)
- [Power Platform CLI](https://learn.microsoft.com/power-platform/developer/cli/introduction)

## Support & Contribution

### Getting Help
- Review Microsoft Learn MCP Server documentation
- Check Azure OpenAI service health
- Consult Power Platform custom connector docs
- Review connector logs in Power Platform admin center

### Reporting Issues
When reporting issues, include:
- Connector version/deployment date
- Sample query (sanitized)
- Error message from response
- Azure OpenAI model and endpoint used
- Learn MCP tool that failed (if applicable)

### Feedback
This connector demonstrates integration of:
- Microsoft Learn MCP Server (public preview)
- Azure OpenAI GPT-4o
- Power Platform Custom Connectors with Code

Feedback on any of these components should be directed to their respective product teams.

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
