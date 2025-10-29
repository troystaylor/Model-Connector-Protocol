# Model Context Protocol (MCP) Template Connector

## Overview

This is a template connector for integrating Model Context Protocol (MCP) servers with Microsoft Power Platform. MCP is a standardized protocol that enables AI applications to connect to various data sources and tools through JSON-RPC 2.0 communication.

## What is MCP?

The Model Context Protocol provides a universal way for AI systems to access context from different sources. It follows a client-server architecture where:

- **MCP Host**: The AI application (e.g., Copilot Studio)
- **MCP Client**: Maintains connection to an MCP server (this connector)
- **MCP Server**: Provides context, tools, and capabilities

### Key Concepts

MCP consists of two layers:

1. **Data Layer**: JSON-RPC 2.0 based protocol for message exchange
2. **Transport Layer**: HTTP/HTTPS communication (this connector uses HTTP POST)

### Primitives

MCP defines several primitives that servers can expose:

- **Tools**: Executable functions (e.g., file operations, API calls)
- **Resources**: Data sources providing context (e.g., file contents, database records)
- **Prompts**: Reusable templates for AI interactions

## Template Usage

This template provides a foundation for connecting to any MCP server. To create a custom connector:

### 1. Configure the OpenAPI Definition (apiDefinition.swagger.json)

Update these fields:

```json
{
  "info": {
    "title": "Your MCP Server Name",
    "description": "Description of your specific MCP server",
    "contact": {
      "name": "Your Name",
      "email": "your.email@example.com"
    }
  },
  "host": "your-actual-mcp-server.com",
  "basePath": "/your/api/path"
}
```

### 2. Update Connector Properties (apiProperties.json)

Modify authentication if needed:

```json
{
  "properties": {
    "connectionParameters": {
      "api_key": {
        "uiDefinition": {
          "displayName": "Your Server API Key",
          "description": "Description specific to your authentication"
        }
      }
    },
    "publisher": "Your Name/Company",
    "stackOwner": "Your MCP Server Name"
  }
}
```

### 3. Customize Authentication

The template uses API Key authentication. For different auth methods:

**Bearer Token**: Already configured - API key is sent as Authorization header

**OAuth 2.0**: Update `securityDefinitions` in apiDefinition.swagger.json:

```json
"securityDefinitions": {
  "oauth2": {
    "type": "oauth2",
    "flow": "accessCode",
    "authorizationUrl": "https://your-server.com/oauth/authorize",
    "tokenUrl": "https://your-server.com/oauth/token",
    "scopes": {
      "read": "Read access",
      "write": "Write access"
    }
  }
}
```

**Custom Headers**: Add policy templates in apiProperties.json

### 4. Custom Code (script.csx)

The script.csx file handles:

- ✅ JSON-RPC 2.0 validation
- ✅ Request forwarding to MCP server
- ✅ Error handling and logging
- ✅ Response validation

**Usually no changes needed** unless you need to:

- Transform requests before sending to server
- Add custom error handling logic
- Implement caching or retry logic
- Add custom logging

## Operation: Invoke MCP Server

This connector provides a single operation that handles all MCP interactions:

### Method: `InvokeMCP`

**Input Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| jsonrpc | string | Yes | Must be "2.0" |
| method | string | Yes | MCP method name (see below) |
| params | object | No | Method-specific parameters |
| id | string/number | No | Request ID (omit for notifications) |

**Supported Methods:**

**Lifecycle:**
- `initialize` - Initialize connection and negotiate capabilities
- `initialized` - Notification that client is ready
- `ping` - Keep-alive check

**Tools:**
- `tools/list` - List available tools
- `tools/call` - Execute a tool

**Resources:**
- `resources/list` - List available resources
- `resources/read` - Read a resource
- `resources/templates/list` - List resource templates
- `resources/subscribe` - Subscribe to resource updates
- `resources/unsubscribe` - Unsubscribe from resource updates

**Prompts:**
- `prompts/list` - List available prompts
- `prompts/get` - Get a specific prompt

**Other:**
- `logging/setLevel` - Set logging level
- `completion/complete` - Request completion
- `roots/list` - List root resources

### Example: Initialize Connection

```json
{
  "jsonrpc": "2.0",
  "method": "initialize",
  "params": {
    "protocolVersion": "2025-06-18",
    "capabilities": {
      "roots": {
        "listChanged": true
      }
    },
    "clientInfo": {
      "name": "PowerPlatform-Copilot",
      "version": "1.0.0"
    }
  },
  "id": 1
}
```

### Example: List Tools

```json
{
  "jsonrpc": "2.0",
  "method": "tools/list",
  "id": 2
}
```

### Example: Call a Tool

```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "get_weather",
    "arguments": {
      "location": "San Francisco",
      "units": "imperial"
    }
  },
  "id": 3
}
```

## Testing Your Connector

1. **Deploy the connector** to your Power Platform environment
2. **Create a connection** with your MCP server credentials
3. **Test initialization**:
   - Call InvokeMCP with `initialize` method
   - Verify you receive server capabilities in response
4. **Discover tools**:
   - Call InvokeMCP with `tools/list` method
   - Review available tools
5. **Execute a tool**:
   - Call InvokeMCP with `tools/call` method
   - Provide tool name and arguments

## Integration with Copilot Studio

To use this connector with Copilot Studio:

### Setting Up the Connection

1. **Deploy the connector** to your Power Platform environment
2. **Create a connection in Power Automate**:
   - Go to Power Automate → Data → Connections
   - Create a new connection using this custom connector
   - Provide your MCP server API key/credentials
3. **Add the connector as a tool in Copilot Studio**:
   - Open your agent in Copilot Studio
   - Go to Tools → Add a tool
   - Select "New tool" → "Model Context Protocol"
   - Select the listing for the custom connector-based MCP server.
   - The tool will automatically discover and expose available MCP tools and resources
4. **Configure discovered tools** with clear descriptions for generative orchestration
5. **Test the integration** by asking your agent questions that trigger the MCP tools

> **Note**: Copilot Studio has native support for Model Context Protocol. When you add an MCP tool, Copilot Studio automatically handles the JSON-RPC communication, capability negotiation, and tool discovery. This template connector is useful for advanced scenarios where you need custom code transformations or when working with MCP servers through Power Automate flows.

### Application Lifecycle Management (ALM)

This connector can be included in Dataverse solutions for proper ALM:

1. **Add to Solution**:
   - In Power Apps or Power Automate, add the custom connector to a solution
   - Add associated connections (as connection references)
   - Add any flows or agent tools that use this connector

2. **Export/Import**:
   - Export the solution containing the connector
   - Import to target environments (Dev → Test → Production)
   - Update connection references in each environment

3. **Best Practices**:
   - Use environment variables for server endpoints if they differ across environments
   - Use connection references instead of direct connections
   - Version your solutions appropriately
   - Document MCP server dependencies in solution notes

### Example Copilot Tool Usage

**Option 1: Native MCP Tool (Recommended)**
- Go to Copilot Studio → Tools → Add a tool → Model Context Protocol
- Enter your MCP server endpoint URL
- Configure authentication (API key/bearer token)
- Copilot Studio automatically:
  - Calls `initialize` to negotiate capabilities
  - Discovers available tools via `tools/list`
  - Exposes each MCP tool as a separate agent tool
  - Handles `tools/call` execution automatically
- Configure tool descriptions for better generative orchestration
- The agent can now automatically use MCP tools in conversations

**Option 2: Custom Connector with Agent Flow (Advanced)**
```
1. Initialize MCP Connection
   └─ Agent Flow step calls InvokeMCP(method: "initialize", params: {...})

2. List Available Tools
   └─ Agent Flow step calls InvokeMCP(method: "tools/list")

3. Parse Tool List
   └─ Parse JSON response to extract tool names and descriptions

4. Call Specific Tool
   └─ Agent Flow step calls InvokeMCP(method: "tools/call", params: {name: "tool_name", arguments: {...}})

5. Return Results to User
   └─ Format and present tool results in Copilot conversation
```

**When to use this custom connector template:**
- You need to transform requests/responses before sending to MCP server
- You want to integrate MCP through Power Automate flows
- You need custom error handling or logging beyond native MCP support
- You're working with a non-standard MCP implementation
- You want to add caching, rate limiting, or other middleware logic

> **Tip**: For most scenarios, use the native "Model Context Protocol" tool type in Copilot Studio. It handles all JSON-RPC communication automatically and provides the best user experience. Use this custom connector template only when you need custom code transformations or advanced integration patterns.

## Error Handling

The connector returns JSON-RPC 2.0 error responses:

| Error Code | Meaning |
|------------|---------|
| -32700 | Parse error (invalid JSON) |
| -32600 | Invalid Request (malformed JSON-RPC) |
| -32601 | Method not found |
| -32602 | Invalid params |
| -32603 | Internal error |
| -32000 to -32099 | Server-defined errors |

Example error response:

```json
{
  "jsonrpc": "2.0",
  "error": {
    "code": -32602,
    "message": "Invalid params",
    "data": {
      "details": "Missing required parameter: location"
    }
  },
  "id": 3
}
```

## References

- [Model Context Protocol Documentation](https://modelcontextprotocol.io/)
- [MCP Architecture](https://modelcontextprotocol.io/docs/learn/architecture)
- [MCP Specification](https://modelcontextprotocol.io/specification/latest)
- [Power Platform Connectors](https://learn.microsoft.com/en-us/connectors/custom-connectors/)
- [Custom Connector Code](https://learn.microsoft.com/en-us/connectors/custom-connectors/write-code)

## Support

For issues with the MCP protocol, refer to the [MCP GitHub repository](https://github.com/modelcontextprotocol).

For issues with this connector template, contact the publisher listed in apiProperties.json.

## License

This template is provided as-is for use with Power Platform custom connectors.
