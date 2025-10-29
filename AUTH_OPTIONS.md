# Authentication Options for MCP Template Connector

This template provides multiple authentication configurations to match your MCP server's requirements. Choose the appropriate files based on your server's authentication method.

## Available Authentication Methods

### 1. No Authentication (Default)
**Files:**
- `apiDefinition.swagger.json`
- `apiProperties.json`

**Use when:**
- Your MCP server is publicly accessible without authentication
- You're testing locally or in a development environment
- Authentication is handled at a different layer (e.g., network level, VPN)

**Configuration:**
- No additional setup required
- Server must be accessible without credentials

---

### 2. API Key Authentication
**Files:**
- `apiDefinition.apikey.swagger.json`
- `apiProperties.apikey.json`

**Use when:**
- Your MCP server uses API key authentication
- Keys are passed in the `X-API-Key` header

**Configuration:**
1. Rename files to remove `.apikey` extension:
   - `apiDefinition.apikey.swagger.json` → `apiDefinition.swagger.json`
   - `apiProperties.apikey.json` → `apiProperties.json`
2. Deploy the connector
3. When creating a connection, enter your API key

**Header sent:** `X-API-Key: your-api-key-here`

---

### 3. Bearer Token Authentication
**Files:**
- `apiDefinition.bearer.swagger.json`
- `apiProperties.bearer.json`

**Use when:**
- Your MCP server uses bearer token authentication
- Tokens are passed in the `Authorization` header with "Bearer" prefix

**Configuration:**
1. Rename files to remove `.bearer` extension:
   - `apiDefinition.bearer.swagger.json` → `apiDefinition.swagger.json`
   - `apiProperties.bearer.json` → `apiProperties.json`
2. Deploy the connector
3. When creating a connection, enter your bearer token

**Header sent:** `Authorization: Bearer your-token-here`

---

### 4. Basic Authentication (Username/Password)
**Files:**
- `apiDefinition.basic.swagger.json`
- `apiProperties.basic.json`

**Use when:**
- Your MCP server uses HTTP Basic Authentication
- You have a username and password

**Configuration:**
1. Rename files to remove `.basic` extension:
   - `apiDefinition.basic.swagger.json` → `apiDefinition.swagger.json`
   - `apiProperties.basic.json` → `apiProperties.json`
2. Deploy the connector
3. When creating a connection, enter your username and password

**Header sent:** `Authorization: Basic base64(username:password)`

---

### 5. OAuth 2.0 Authentication
**Files:**
- `apiDefinition.oauth.swagger.json`
- `apiProperties.oauth.json`

**Use when:**
- Your MCP server uses OAuth 2.0 for authentication
- You need delegated user authorization

**Configuration:**
1. Rename files to remove `.oauth` extension:
   - `apiDefinition.oauth.swagger.json` → `apiDefinition.swagger.json`
   - `apiProperties.oauth.json` → `apiProperties.json`
2. Update OAuth URLs in both files:
   - Authorization URL: `https://your-server.com/oauth/authorize`
   - Token URL: `https://your-server.com/oauth/token`
   - Refresh URL: `https://your-server.com/oauth/token`
3. Update Client ID in `apiProperties.json`
4. Configure scopes as needed
5. Deploy the connector
6. When creating a connection, complete the OAuth flow

**Header sent:** `Authorization: Bearer oauth-access-token`

---

## How to Switch Authentication Methods

1. **Backup current files** (if you've made changes)
2. **Copy the desired authentication files:**
   ```powershell
   # Example: Switch to Bearer Token auth
   Copy-Item apiDefinition.bearer.swagger.json apiDefinition.swagger.json -Force
   Copy-Item apiProperties.bearer.json apiProperties.json -Force
   ```
3. **Update server-specific settings:**
   - Host/endpoint URL
   - OAuth URLs (if using OAuth)
   - Client ID (if using OAuth)
4. **Deploy the connector** to your Power Platform environment
5. **Create a new connection** with the appropriate credentials

## Customizing Authentication

If your MCP server uses a different authentication method, you can customize the template:

### Custom Headers
Edit `apiProperties.json` and add policy templates:

```json
"policyTemplateInstances": [
  {
    "templateId": "setheader",
    "title": "Set Custom Header",
    "parameters": {
      "x-ms-apimTemplateParameter.name": "X-Custom-Header",
      "x-ms-apimTemplateParameter.value": "@connectionParameters('custom_param')",
      "x-ms-apimTemplateParameter.existsAction": "override",
      "x-ms-apimTemplate-policySection": "Request"
    }
  }
]
```

### Query Parameters
For authentication via query parameters, modify the `apiDefinition.swagger.json`:

```json
"parameters": [
  {
    "name": "api_key",
    "in": "query",
    "required": true,
    "type": "string",
    "x-ms-summary": "API Key"
  }
]
```

## Testing Authentication

After configuring authentication:

1. Deploy the connector to your environment
2. Create a test connection with valid credentials
3. Use Power Automate or Copilot Studio to test the `InvokeMCP` operation
4. Try calling `initialize` method to verify authentication works
5. Check for 401/403 errors if authentication fails

## Security Best Practices

- **Never commit credentials** to source control
- Use **environment variables** for different environments (dev/test/prod)
- Store API keys and tokens in **Azure Key Vault** when possible
- Use **OAuth 2.0** for production deployments when available
- Regularly **rotate credentials** and update connections
- Use **connection references** in solutions for proper ALM
- Configure **least privilege** scopes for OAuth

## Troubleshooting

| Error | Likely Cause | Solution |
|-------|--------------|----------|
| 401 Unauthorized | Invalid credentials | Verify API key/token/password is correct |
| 403 Forbidden | Insufficient permissions | Check OAuth scopes or API key permissions |
| Invalid header format | Wrong auth configuration | Ensure you're using the correct auth method files |
| Token expired | OAuth token needs refresh | Reconnect or configure refresh token URL |

## Support

For authentication issues:
1. Verify your MCP server's authentication requirements
2. Check the connector's connection test
3. Review Power Platform connector logs
4. Consult your MCP server documentation
