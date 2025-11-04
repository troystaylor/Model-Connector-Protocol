using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// Model Context Protocol (MCP) Custom Code Connector
/// Implements MCP server with AI agent orchestration for Power Platform
/// See readme.md for detailed documentation
/// </summary>
public class Script : ScriptBase
{
    // ============================================================================
    // DEVELOPER CONFIGURATION: Set to false if using AI agent in existing connector
    // ============================================================================
    // When true: All operations route to AI agent (standalone MCP connector)
    // When false: Only "InvokeMCP" operation uses AI agent (hybrid connector with other API operations)
    private const bool AI_AGENT_ONLY = true;

    // AI Orchestration Configuration
    private const string DEFAULT_BASE_URL = "https://ai-troy2981ai949275546385.openai.azure.com";
    private const string DEFAULT_MODEL = "gpt-4o";
    private const int DEFAULT_MAX_TOKENS = 16000;
    private const double DEFAULT_TEMPERATURE = 0.7;
    private const int DEFAULT_MAX_TOOL_CALLS = 10;

    // ============================================================================
    // DEVELOPER CONFIGURATION: CUSTOMIZE AI BEHAVIOR HERE
    // ============================================================================
    /// <summary>
    /// Define your system instructions here to guide how the AI processes requests.
    /// These instructions control the AI's behavior, tone, and approach.
    /// Customize this to align with your specific use case and brand voice.
    /// </summary>
    private string GetSystemInstructions()
    {
        // ============================================================================
        // CUSTOMIZE THESE INSTRUCTIONS FOR YOUR USE CASE
        // Define the AI's role, behavior guidelines, response style, and constraints.
        // These instructions are sent as the system prompt on every request.
        // ============================================================================
        return @"
            You are an intelligent AI assistant with access to tools and resources through the Model Context Protocol (MCP).

            Your Behavior:
            - Analyze requests carefully and use available tools to provide accurate, actionable information
            - Be clear, concise, and professional in your responses
            - When using tools, explain what you're doing and synthesize the results into a coherent response
            - If you need more information to complete a request, ask specific clarifying questions
            - Cite sources when using resource content in your responses
            - Handle errors gracefully and suggest alternative approaches when tools fail

            Response Style:
            - Use a professional yet approachable tone
            - Structure responses with clear sections when appropriate
            - Prioritize accuracy over speed - take time to use tools correctly
            - Be transparent about limitations and uncertainties";
    }

    /// <summary>
    /// Gets the server information returned during initialization
    /// </summary>
    private JObject GetServerInfo()
    {
        return new JObject
        {
            ["name"] = "model-connector-protocol-server",
            ["version"] = "1.0.0"
        };
    }

    /// <summary>
    /// Gets the server capabilities exposed to clients
    /// </summary>
    private JObject GetServerCapabilities()
    {
        return new JObject
        {
            ["tools"] = new JObject
            {
                ["listChanged"] = false // Set to true if tools can change at runtime
            },
            ["resources"] = new JObject
            {
                ["subscribe"] = false, // HTTP/HTTPS resources don't support subscriptions
                ["listChanged"] = false // Set to true if resource list can change at runtime
            },
            ["prompts"] = new JObject
            {
                ["listChanged"] = false // Set to true if prompt list can change at runtime
            }
        };
    }

    /// <summary>
    /// Defines the MCP tools available through this connector.
    /// Add your custom tools here that map to external API operations.
    /// Each tool must have: name (string), description (string), and inputSchema (JSON Schema object)
    /// 
    /// IMPORTANT: For each tool you add here, you must also implement a corresponding handler method
    /// in ExecuteToolByName() with the pattern: Execute{ToolName}Tool(JObject arguments)
    /// 
    /// Expected schema format:
    /// new JObject
    /// {
    ///     ["name"] = "tool_name",
    ///     ["description"] = "What this tool does",
    ///     ["inputSchema"] = new JObject
    ///     {
    ///         ["type"] = "object",
    ///         ["properties"] = new JObject
    ///         {
    ///             ["param1"] = new JObject
    ///             {
    ///                 ["type"] = "string",
    ///                 ["description"] = "Parameter description"
    ///             }
    ///         },
    ///         ["required"] = new JArray { "param1" }
    ///     }
    /// }
    /// </summary>
    private JArray GetDefinedTools()
    {
        return new JArray
        {
            // Add your tools here
        };
    }

    /// <summary>
    /// Defines the MCP resources available through this connector.
    /// Resources provide read-only access to data via HTTP/HTTPS URLs.
    /// Add your custom resources here that map to public web endpoints.
    /// Each resource must have: uri (HTTP/HTTPS URL), name (string), description (string), mimeType (string)
    /// 
    /// Expected schema format:
    /// new JObject
    /// {
    ///     ["uri"] = "https://api.example.com/config/settings.json",
    ///     ["name"] = "Application Settings",
    ///     ["description"] = "Current application configuration from API",
    ///     ["mimeType"] = "application/json"
    /// }
    /// </summary>
    private JArray GetDefinedResources()
    {
        return new JArray
        {
            // Add your resources here
        };
    }



    /// <summary>
    /// Defines the MCP prompts available through this connector.
    /// Prompts are reusable templates with parameters that guide AI interactions.
    /// Each prompt must have: name (string), description (string), arguments (array of argument objects)
    /// Each argument must have: name (string), description (string), required (boolean)
    /// 
    /// Expected schema format:
    /// new JObject
    /// {
    ///     ["name"] = "analyze_data",
    ///     ["description"] = "Analyze data from a specific source",
    ///     ["arguments"] = new JArray
    ///     {
    ///         new JObject
    ///         {
    ///             ["name"] = "source",
    ///             ["description"] = "Data source to analyze",
    ///             ["required"] = true
    ///         }
    ///     }
    /// }
    /// </summary>
    private JArray GetDefinedPrompts()
    {
        return new JArray
        {
            // Add your prompts here
        };
    }

    /// <summary>
    /// Main entry point for the custom connector
    /// Routes requests to either AI Agent mode or traditional MCP mode
    /// </summary>
    public override async Task<HttpResponseMessage> ExecuteAsync()
    {
        // Log the incoming operation
        this.Context.Logger?.LogInformation($"Connector: Processing operation {this.Context.OperationId}");

        // Check if AI agent handles all operations or just InvokeMCP
        if (AI_AGENT_ONLY || this.Context.OperationId == "InvokeMCP")
        {
            return await RouteRequest().ConfigureAwait(false);
        }

        // For hybrid connectors: Handle other API operations here
        // Example:
        // switch (this.Context.OperationId)
        // {
        //     case "GetUser":
        //         return await HandleGetUser().ConfigureAwait(false);
        //     case "CreateOrder":
        //         return await HandleCreateOrder().ConfigureAwait(false);
        //     default:
        //         break;
        // }

        // Handle unknown operation
        var errorResponse = new HttpResponseMessage(HttpStatusCode.BadRequest);
        errorResponse.Content = CreateJsonContent(new JObject
        {
            ["error"] = "Unknown operation",
            ["message"] = $"Operation '{this.Context.OperationId}' is not supported"
        }.ToString());

        return errorResponse;
    }

    /// <summary>
    /// Routes requests to appropriate handler based on request format
    /// Detects if it's an AI Agent request or traditional MCP JSON-RPC request
    /// </summary>
    private async Task<HttpResponseMessage> RouteRequest()
    {
        try
        {
            var requestBody = await this.Context.Request.Content.ReadAsStringAsync().ConfigureAwait(false);
            this.Context.Logger?.LogInformation($"Request Body: {requestBody}");

            JObject requestJson;
            try
            {
                requestJson = JObject.Parse(requestBody);
            }
            catch (JsonException ex)
            {
                this.Context.Logger?.LogError($"Invalid JSON: {ex.Message}");
                return CreateAgentErrorResponse("Invalid JSON in request body", ex.Message, "ValidationError", "INVALID_JSON");
            }

            // Detect request type: MCP (has jsonrpc) vs Agent (has mode/input)
            if (requestJson.ContainsKey("jsonrpc"))
            {
                // Traditional MCP JSON-RPC request
                this.Context.Logger?.LogInformation("Routing to MCP handler");
                return await HandleMCPRequest(requestJson).ConfigureAwait(false);
            }
            else if (requestJson.ContainsKey("mode") || requestJson.ContainsKey("input"))
            {
                // AI Agent request
                this.Context.Logger?.LogInformation("Routing to AI Agent handler");
                return await HandleAgentRequest(requestJson).ConfigureAwait(false);
            }
            else
            {
                // Ambiguous request - try to infer
                if (requestJson.ContainsKey("method"))
                {
                    // Looks like MCP without jsonrpc field - add it and process
                    requestJson["jsonrpc"] = "2.0";
                    return await HandleMCPRequest(requestJson).ConfigureAwait(false);
                }
                else
                {
                    return CreateAgentErrorResponse("Could not determine request type. Include 'mode' for Agent mode or 'jsonrpc' for MCP mode.", null);
                }
            }
        }
        catch (Exception ex)
        {
            this.Context.Logger?.LogError($"Routing error: {ex.Message}\n{ex.StackTrace}");
            return CreateAgentErrorResponse($"Request routing failed: {ex.Message}", null);
        }
    }

    /// <summary>
    /// Handles MCP JSON-RPC requests
    /// Validates the request format and processes MCP protocol methods
    /// </summary>
    private async Task<HttpResponseMessage> HandleMCPRequest(JObject requestJson)
    {
        try
        {
            this.Context.Logger?.LogInformation("Processing MCP request");

            // Validate JSON-RPC 2.0 format
            var validationError = ValidateJsonRpcRequest(requestJson);
            if (validationError != null)
            {
                return validationError;
            }

            // Extract method and parameters
            string method = requestJson["method"]?.ToString();
            var requestId = requestJson["id"];
            bool isNotification = requestId == null;

            this.Context.Logger?.LogInformation($"MCP Method: {method}, IsNotification: {isNotification}");

            // Handle methods locally based on defined tools, resources, and prompts
            switch (method)
            {
                // Lifecycle methods
                case "initialize":
                    return HandleInitialize(requestJson, requestId);

                case "initialized":
                case "ping":
                    return CreateSuccessResponse(new JObject(), requestId);

                // Tool methods
                case "tools/list":
                    return HandleToolsList(requestId);

                case "tools/call":
                    return await HandleToolsCall(requestJson, requestId).ConfigureAwait(false);

                // Resource methods
                case "resources/list":
                    return HandleResourcesList(requestId);

                case "resources/read":
                    return await HandleResourcesRead(requestJson, requestId).ConfigureAwait(false);

                case "resources/subscribe":
                case "resources/unsubscribe":
                    // Resource subscriptions are not supported in synchronous Power Platform connectors
                    return CreateErrorResponse(-32601, "Resource subscriptions not supported. Power Platform connectors are synchronous and cannot maintain persistent connections for push notifications.", requestId);

                // Prompt methods
                case "prompts/list":
                    return HandlePromptsList(requestId);

                case "prompts/get":
                    return HandlePromptsGet(requestJson, requestId);

                // Completion method (for prompt arguments)
                case "completion/complete":
                    return await HandleCompletionComplete(requestJson, requestId).ConfigureAwait(false);

                default:
                    // For any unhandled methods, return method not found
                    return CreateErrorResponse(-32601, $"Method not found: {method}", requestId);
            }
        }
        catch (Exception ex)
        {
            this.Context.Logger?.LogError($"Unexpected error in MCP handler: {ex.Message}");
            return CreateErrorResponse(-32603, $"Internal error: {ex.Message}", null);
        }
    }

    /// <summary>
    /// Handles AI Agent requests with orchestration and generation capabilities
    /// </summary>
    private async Task<HttpResponseMessage> HandleAgentRequest(JObject requestJson)
    {
        var startTime = DateTime.UtcNow;
        var executionMetadata = new JObject();

        try
        {
            // Extract request parameters
            var input = requestJson["input"]?.ToString();
            var options = requestJson["options"] as JObject ?? new JObject();

            if (string.IsNullOrEmpty(input))
            {
                return CreateAgentErrorResponse("'input' field is required", "The request body must include an 'input' field with your question or request.", "ValidationError", "MISSING_INPUT");
            }

            this.Context.Logger?.LogInformation($"Agent request - Input: {input}");

            // Extract options with defaults
            var autoExecuteTools = options["autoExecuteTools"]?.Value<bool>() ?? true;
            var maxToolCalls = options["maxToolCalls"]?.Value<int>() ?? DEFAULT_MAX_TOOL_CALLS;
            var includeToolResults = options["includeToolResults"]?.Value<bool>() ?? true;
            var temperature = options["temperature"]?.Value<double>() ?? DEFAULT_TEMPERATURE;
            var maxTokens = options["maxTokens"]?.Value<int>() ?? DEFAULT_MAX_TOKENS;

            // Get AI model API key from connection parameters
            var apiKey = this.Context.Request.Headers.TryGetValues("ai_api_key", out var apiKeyValues) 
                ? apiKeyValues.FirstOrDefault() 
                : null;

            var baseUrl = DEFAULT_BASE_URL;

            var model = options["model"]?.ToString();
            if (string.IsNullOrEmpty(model) || model == "")
            {
                model = DEFAULT_MODEL;
            }

            if (string.IsNullOrEmpty(apiKey))
            {
                return CreateAgentErrorResponse(
                    "API key not configured. Please ensure you entered the AI Model API Key when creating the connection.",
                    "The AI API key should be provided via the connector's connection parameters",
                    "AuthenticationError",
                    "MISSING_API_KEY"
                );
            }

            // Call smart agent handler
            var agentResponse = await HandleSmartAgent(input, apiKey, model, baseUrl, temperature, maxTokens, autoExecuteTools, maxToolCalls, includeToolResults).ConfigureAwait(false);

            // Add metadata
            var duration = (DateTime.UtcNow - startTime).TotalSeconds;
            agentResponse["metadata"] = new JObject
            {
                ["duration"] = $"{duration:F2}s",
                ["model"] = model
            };

            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = CreateJsonContent(agentResponse.ToString());
            return response;
        }
        catch (Exception ex)
        {
            this.Context.Logger?.LogError($"Agent request error: {ex.Message}\n{ex.StackTrace}");
            
            // Categorize error types
            var errorType = "AgentError";
            var errorCode = "AGENT_EXECUTION_FAILED";
            
            if (ex.Message.Contains("AI API error"))
            {
                errorType = "ModelError";
                errorCode = "MODEL_API_ERROR";
            }
            else if (ex.Message.Contains("Tool execution failed"))
            {
                errorType = "ToolExecutionError";
                errorCode = "TOOL_EXECUTION_FAILED";
            }
            else if (ex.Message.Contains("API key"))
            {
                errorType = "AuthenticationError";
                errorCode = "INVALID_API_KEY";
            }
            
            return CreateAgentErrorResponse($"Agent execution failed: {ex.Message}", ex.StackTrace, errorType, errorCode);
        }
    }

    /// <summary>
    /// Smart agent handler - Intelligently processes requests with tools
    /// </summary>
    private async Task<JObject> HandleSmartAgent(string input, string apiKey, string model, string baseUrl, double temperature, int maxTokens, bool autoExecute, int maxToolCalls, bool includeResults)
    {
        this.Context.Logger?.LogInformation("Smart Agent: Processing request");

        // Get available tools
        var tools = GetDefinedTools();

        // Build system prompt with developer-defined instructions
        var systemPrompt = GetSystemInstructions();

        // Build messages (stateless - no conversation history)
        var messages = new JArray
        {
            new JObject { ["role"] = "system", ["content"] = systemPrompt },
            new JObject { ["role"] = "user", ["content"] = input }
        };

        // Convert MCP tools to function calling format
        var functions = ConvertMCPToolsToFunctions(tools);

        // Call AI with function calling
        var (aiResponse, executedTools, tokensUsed) = await ExecuteAIWithFunctions(
            apiKey, model, baseUrl, messages, functions, temperature, maxTokens, autoExecute, maxToolCalls
        ).ConfigureAwait(false);

        return new JObject
        {
            ["response"] = aiResponse,
            ["execution"] = new JObject
            {
                ["toolCalls"] = includeResults ? executedTools : new JArray(),
                ["toolsExecuted"] = executedTools.Count
            },
            ["metadata"] = new JObject
            {
                ["tokensUsed"] = tokensUsed
            },
            ["error"] = null
        };
    }

    /// <summary>
    /// Executes AI API call with function calling support (works with OpenAI and Anthropic)
    /// </summary>
    private async Task<(string response, JArray toolCalls, int tokensUsed)> ExecuteAIWithFunctions(
        string apiKey, string model, string baseUrl, JArray messages, JArray functions,
        double temperature, int maxTokens, bool autoExecute, int maxIterations)
    {
        var allToolCalls = new JArray();
        var totalTokens = 0;
        var iterations = 0;

        while (iterations < maxIterations)
        {
            iterations++;
            this.Context.Logger?.LogInformation($"AI API call iteration {iterations}");

            // Build request
            var requestBody = new JObject
            {
                ["model"] = model,
                ["messages"] = messages,
                ["temperature"] = temperature,
                ["max_tokens"] = maxTokens
            };

            if (functions != null && functions.Count > 0)
            {
                requestBody["tools"] = functions;
                requestBody["tool_choice"] = "auto";
            }

            // Call Azure OpenAI API
            var apiVersion = "2024-08-01-preview";
            var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/openai/deployments/{model}/chat/completions?api-version={apiVersion}");
            request.Headers.Add("api-key", apiKey);
            request.Content = new StringContent(requestBody.ToString(), System.Text.Encoding.UTF8, "application/json");

            var response = await this.Context.SendAsync(request, this.CancellationToken).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                this.Context.Logger?.LogError($"AI API error: {responseBody}");
                throw new Exception($"AI API error: {response.StatusCode} - {responseBody}");
            }

            var responseJson = JObject.Parse(responseBody);
            var usage = responseJson["usage"];
            totalTokens += usage?["total_tokens"]?.Value<int>() ?? 0;

            var choice = responseJson["choices"]?[0];
            var message = choice?["message"] as JObject;
            var toolCalls = message?["tool_calls"] as JArray;

            // If no tool calls, return the response
            if (toolCalls == null || toolCalls.Count == 0)
            {
                var content = message?["content"]?.ToString() ?? "";
                return (content, allToolCalls, totalTokens);
            }

            // Execute tool calls if auto-execute is enabled
            if (!autoExecute)
            {
                return ("Tool calls ready but auto-execute is disabled", allToolCalls, totalTokens);
            }

            // Add assistant message with tool calls to conversation
            messages.Add(message);

            // Execute each tool call
            foreach (var toolCall in toolCalls)
            {
                var toolCallId = toolCall["id"]?.ToString();
                var function = toolCall["function"] as JObject;
                var functionName = function?["name"]?.ToString();
                var argumentsStr = function?["arguments"]?.ToString();

                this.Context.Logger?.LogInformation($"Executing tool: {functionName}");

                JObject arguments;
                try
                {
                    arguments = JObject.Parse(argumentsStr);
                }
                catch
                {
                    arguments = new JObject();
                }

                // Execute the tool
                JObject toolResult;
                bool success = true;
                string errorMsg = null;

                try
                {
                    toolResult = await ExecuteToolByName(functionName, arguments).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    success = false;
                    errorMsg = ex.Message;
                    toolResult = new JObject
                    {
                        ["error"] = ex.Message
                    };
                }

                // Record tool call
                allToolCalls.Add(new JObject
                {
                    ["tool"] = functionName,
                    ["arguments"] = arguments,
                    ["result"] = toolResult,
                    ["success"] = success,
                    ["error"] = errorMsg
                });

                // Add tool result to conversation
                messages.Add(new JObject
                {
                    ["role"] = "tool",
                    ["tool_call_id"] = toolCallId,
                    ["content"] = toolResult.ToString()
                });
            }

            // Continue loop to get AI's response after tool execution
        }

        // Max iterations reached
        return ("Max tool call iterations reached", allToolCalls, totalTokens);
    }

    /// <summary>
    /// Executes AI API call without function calling (pure generation)
    /// </summary>
    private async Task<(string response, int tokensUsed)> ExecuteAICompletion(
        string apiKey, string model, string baseUrl, JArray messages, double temperature, int maxTokens)
    {
        var requestBody = new JObject
        {
            ["model"] = model,
            ["messages"] = messages,
            ["temperature"] = temperature,
            ["max_tokens"] = maxTokens
        };

        var apiVersion = "2024-08-01-preview";
        var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/openai/deployments/{model}/chat/completions?api-version={apiVersion}");
        request.Headers.Add("api-key", apiKey);
        request.Content = new StringContent(requestBody.ToString(), System.Text.Encoding.UTF8, "application/json");

        var response = await this.Context.SendAsync(request, this.CancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            this.Context.Logger?.LogError($"AI API error: {responseBody}");
            throw new Exception($"AI API error: {response.StatusCode} - {responseBody}");
        }

        var responseJson = JObject.Parse(responseBody);
        var usage = responseJson["usage"];
        var totalTokens = usage?["total_tokens"]?.Value<int>() ?? 0;
        var content = responseJson["choices"]?[0]?["message"]?["content"]?.ToString() ?? "";

        return (content, totalTokens);
    }

    /// <summary>
    /// Converts MCP tool definitions to function calling format (compatible with OpenAI and Anthropic)
    /// </summary>
    private JArray ConvertMCPToolsToFunctions(JArray mcpTools)
    {
        var functions = new JArray();

        foreach (var tool in mcpTools)
        {
            var function = new JObject
            {
                ["type"] = "function",
                ["function"] = new JObject
                {
                    ["name"] = tool["name"],
                    ["description"] = tool["description"],
                    ["parameters"] = tool["inputSchema"]
                }
            };
            functions.Add(function);
        }

        return functions;
    }

    /// <summary>
    /// Executes a tool by name (routes to appropriate tool handler)
    /// When you add a new tool to GetDefinedTools(), add its handler here.
    /// </summary>
    private async Task<JObject> ExecuteToolByName(string toolName, JObject arguments)
    {
        // Route tool name to handler method
        // Add cases here for each tool defined in GetDefinedTools()
        switch (toolName)
        {
            case "get_weather":
                return await ExecuteGetWeatherTool(arguments).ConfigureAwait(false);

            case "search_data":
                return await ExecuteSearchDataTool(arguments).ConfigureAwait(false);

            // Add more tool handlers here as you add tools to GetDefinedTools()
            // case "your_tool_name":
            //     return await ExecuteYourToolNameTool(arguments).ConfigureAwait(false);

            default:
                // This should never be reached due to validation in HandleToolsCall,
                // but included for safety
                throw new Exception($"Unknown tool: {toolName}");
        }
    }

    /// <summary>
    /// Reads a resource by URI (supports HTTP/HTTPS URLs only)
    /// </summary>
    private async Task<(JObject content, string mimeType)> ReadResourceByUri(string uri)
    {
        if (!uri.StartsWith("http://") && !uri.StartsWith("https://"))
        {
            throw new ArgumentException($"Only HTTP/HTTPS URLs are supported. URI: {uri}");
        }

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            var response = await this.Context.SendAsync(request, this.CancellationToken).ConfigureAwait(false);
            
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to fetch resource: {response.StatusCode}");
            }

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            // Try to parse as JSON if content type suggests it
            if (contentType.Contains("json"))
            {
                try
                {
                    var jsonContent = JObject.Parse(content);
                    return (jsonContent, contentType);
                }
                catch
                {
                    // If JSON parsing fails, return as text with actual content type
                    return (new JObject 
                    { 
                        ["type"] = "text",
                        ["content"] = content,
                        ["originalContentType"] = contentType
                    }, contentType);
                }
            }
            
            // For non-JSON content, determine best representation
            // Text-based content types: return as-is with metadata
            if (contentType.Contains("text") || 
                contentType.Contains("xml") || 
                contentType.Contains("html") ||
                contentType.Contains("csv"))
            {
                return (new JObject 
                { 
                    ["type"] = "text",
                    ["content"] = content,
                    ["contentType"] = contentType
                }, contentType);
            }
            
            // Binary or unknown content: base64 encode or return limited info
            return (new JObject 
            { 
                ["type"] = "binary",
                ["contentType"] = contentType,
                ["length"] = content.Length,
                ["note"] = "Binary content not fully supported in text response"
            }, contentType);
        }
        catch (Exception ex)
        {
            this.Context.Logger?.LogError($"Error reading resource {uri}: {ex.Message}");
            throw new Exception($"Failed to read resource: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Handles the initialize request and returns server capabilities
    /// </summary>
    private HttpResponseMessage HandleInitialize(JObject requestJson, JToken requestId)
    {
        this.Context.Logger?.LogInformation("Handling initialize request");

        // Extract client info from params if provided
        var clientParams = requestJson["params"] as JObject;
        var clientInfo = clientParams?["clientInfo"];
        var protocolVersion = clientParams?["protocolVersion"]?.ToString() ?? "2025-06-18";

        this.Context.Logger?.LogInformation($"Client: {clientInfo}, Protocol: {protocolVersion}");

        var result = new JObject
        {
            ["protocolVersion"] = protocolVersion,
            ["capabilities"] = GetServerCapabilities(),
            ["serverInfo"] = GetServerInfo()
        };

        return CreateSuccessResponse(result, requestId);
    }

    /// <summary>
    /// Handles the tools/list request and returns defined tools
    /// </summary>
    private HttpResponseMessage HandleToolsList(JToken requestId)
    {
        this.Context.Logger?.LogInformation("Handling tools/list request");

        var result = new JObject
        {
            ["tools"] = GetDefinedTools()
        };

        return CreateSuccessResponse(result, requestId);
    }

    /// <summary>
    /// Handles the tools/call request and executes the specified tool
    /// </summary>
    private async Task<HttpResponseMessage> HandleToolsCall(JObject requestJson, JToken requestId)
    {
        this.Context.Logger?.LogInformation("Handling tools/call request");

        try
        {
            var paramsObj = requestJson["params"] as JObject;
            if (paramsObj == null)
            {
                return CreateErrorResponse(-32602, "Invalid params: 'params' object required", requestId);
            }

            var toolName = paramsObj["name"]?.ToString();
            if (string.IsNullOrEmpty(toolName))
            {
                return CreateErrorResponse(-32602, "Invalid params: 'name' field required", requestId);
            }

            // Validate arguments is either null or JObject (not other types)
            var arguments = paramsObj["arguments"] as JObject;
            if (paramsObj.ContainsKey("arguments") && arguments == null && paramsObj["arguments"] != null)
            {
                return CreateErrorResponse(-32602, "Invalid params: 'arguments' must be an object", requestId);
            }

            this.Context.Logger?.LogInformation($"Calling tool: {toolName}");

            // Check if tool exists in defined tools
            var definedTools = GetDefinedTools();
            var toolDefinition = definedTools.FirstOrDefault(t => t["name"]?.ToString() == toolName);
            
            if (toolDefinition == null)
            {
                // Use -32601 (Method not found) for unknown tools per JSON-RPC spec
                return CreateErrorResponse(-32601, $"Unknown tool: {toolName}", requestId);
            }

            // Execute the tool
            JObject toolResult;
            try
            {
                toolResult = await ExecuteToolByName(toolName, arguments).ConfigureAwait(false);

                // Return successful tool execution result
                var result = new JObject
                {
                    ["content"] = new JArray
                    {
                        new JObject
                        {
                            ["type"] = "text",
                            ["text"] = toolResult.ToString()
                        }
                    }
                };

                return CreateSuccessResponse(result, requestId);
            }
            catch (ArgumentException ex)
            {
                // Invalid parameters provided to tool
                this.Context.Logger?.LogError($"Tool '{toolName}' argument error: {ex.Message}");
                return CreateErrorResponse(-32602, $"Invalid tool arguments: {ex.Message}", requestId);
            }
            catch (HttpRequestException ex)
            {
                // Network/API errors
                this.Context.Logger?.LogError($"Tool '{toolName}' network error: {ex.Message}\n{ex.StackTrace}");
                return CreateErrorResponse(-32000, $"Tool execution failed due to network error: {ex.Message}", requestId);
            }
            catch (JsonException ex)
            {
                // JSON parsing errors
                this.Context.Logger?.LogError($"Tool '{toolName}' JSON error: {ex.Message}\n{ex.StackTrace}");
                return CreateErrorResponse(-32700, $"Failed to parse tool response: {ex.Message}", requestId);
            }
            catch (TaskCanceledException ex)
            {
                // Timeout errors
                this.Context.Logger?.LogError($"Tool '{toolName}' timeout: {ex.Message}");
                return CreateErrorResponse(-32000, $"Tool execution timed out: {ex.Message}", requestId);
            }
            catch (Exception ex)
            {
                // Unexpected errors
                this.Context.Logger?.LogError($"Tool '{toolName}' unexpected error: {ex.Message}\n{ex.StackTrace}");
                return CreateErrorResponse(-32000, $"Tool execution failed: {ex.Message}", requestId, new JObject
                {
                    ["toolName"] = toolName,
                    ["errorType"] = ex.GetType().Name
                });
            }
        }
        catch (Exception ex)
        {
            // Outer catch for any validation errors
            this.Context.Logger?.LogError($"Error handling tools/call: {ex.Message}\n{ex.StackTrace}");
            return CreateErrorResponse(-32603, $"Internal error: {ex.Message}", requestId);
        }
    }

    /// <summary>
    /// Handles the resources/list request and returns defined resources
    /// </summary>
    private HttpResponseMessage HandleResourcesList(JToken requestId)
    {
        this.Context.Logger?.LogInformation("Handling resources/list request");

        try
        {
            var result = new JObject
            {
                ["resources"] = GetDefinedResources()
            };

            return CreateSuccessResponse(result, requestId);
        }
        catch (Exception ex)
        {
            this.Context.Logger?.LogError($"Error listing resources: {ex.Message}");
            return CreateErrorResponse(-32603, $"Failed to list resources: {ex.Message}", requestId);
        }
    }

    /// <summary>
    /// Handles the resources/read request and returns resource contents
    /// </summary>
    private async Task<HttpResponseMessage> HandleResourcesRead(JObject requestJson, JToken requestId)
    {
        this.Context.Logger?.LogInformation("Handling resources/read request");

        try
        {
            var paramsObj = requestJson["params"] as JObject;
            if (paramsObj == null)
            {
                return CreateErrorResponse(-32602, "Invalid params: 'params' object required", requestId);
            }

            var uri = paramsObj["uri"]?.ToString();
            if (string.IsNullOrEmpty(uri))
            {
                return CreateErrorResponse(-32602, "Invalid params: 'uri' field required", requestId);
            }

            this.Context.Logger?.LogInformation($"Reading resource: {uri}");

            // Read the resource via HTTP/HTTPS
            JObject resourceContent;
            string mimeType;

            try
            {
                (resourceContent, mimeType) = await ReadResourceByUri(uri).ConfigureAwait(false);
            }
            catch (ArgumentException ex)
            {
                // Invalid URI (not HTTP/HTTPS)
                return CreateErrorResponse(-32602, $"Invalid resource URI: {ex.Message}", requestId);
            }
            catch (HttpRequestException ex)
            {
                // Network/fetch error
                return CreateErrorResponse(-32000, $"Failed to fetch resource: {ex.Message}", requestId);
            }
            catch (Exception ex)
            {
                // Unexpected error
                this.Context.Logger?.LogError($"Resource read error: {ex.Message}\n{ex.StackTrace}");
                return CreateErrorResponse(-32000, $"Resource read failed: {ex.Message}", requestId);
            }

            // Return resource contents in MCP format
            // Extract text content based on the structure returned by ReadResourceByUri
            string textContent;
            
            if (resourceContent.ContainsKey("type"))
            {
                // Wrapped format (text/binary with metadata)
                var contentType = resourceContent["type"]?.ToString();
                if (contentType == "text")
                {
                    // Extract the actual text content
                    textContent = resourceContent["content"]?.ToString() ?? resourceContent.ToString(Newtonsoft.Json.Formatting.None);
                }
                else
                {
                    // Binary or other - return the whole metadata object as JSON string
                    textContent = resourceContent.ToString(Newtonsoft.Json.Formatting.None);
                }
            }
            else
            {
                // Pure JSON content - serialize it
                textContent = resourceContent.ToString(Newtonsoft.Json.Formatting.None);
            }

            var result = new JObject
            {
                ["contents"] = new JArray
                {
                    new JObject
                    {
                        ["uri"] = uri,
                        ["mimeType"] = mimeType,
                        ["text"] = textContent
                    }
                }
            };

            return CreateSuccessResponse(result, requestId);
        }
        catch (ArgumentException ex)
        {
            this.Context.Logger?.LogError($"Invalid resource parameter: {ex.Message}");
            return CreateErrorResponse(-32602, $"Invalid params: {ex.Message}", requestId);
        }
        catch (Exception ex)
        {
            this.Context.Logger?.LogError($"Resource read error: {ex.Message}");
            return CreateErrorResponse(-32000, $"Resource read failed: {ex.Message}", requestId);
        }
    }

    /// <summary>
    /// Handles the prompts/list request and returns defined prompts
    /// </summary>
    private HttpResponseMessage HandlePromptsList(JToken requestId)
    {
        this.Context.Logger?.LogInformation("Handling prompts/list request");

        try
        {
            var result = new JObject
            {
                ["prompts"] = GetDefinedPrompts()
            };

            return CreateSuccessResponse(result, requestId);
        }
        catch (Exception ex)
        {
            this.Context.Logger?.LogError($"Error listing prompts: {ex.Message}");
            return CreateErrorResponse(-32603, $"Failed to list prompts: {ex.Message}", requestId);
        }
    }

    /// <summary>
    /// Handles the prompts/get request and returns prompt details with messages
    /// </summary>
    private HttpResponseMessage HandlePromptsGet(JObject requestJson, JToken requestId)
    {
        this.Context.Logger?.LogInformation("Handling prompts/get request");

        try
        {
            var paramsObj = requestJson["params"] as JObject;
            if (paramsObj == null)
            {
                return CreateErrorResponse(-32602, "Invalid params: 'params' object required", requestId);
            }

            var promptName = paramsObj["name"]?.ToString();
            if (string.IsNullOrEmpty(promptName))
            {
                return CreateErrorResponse(-32602, "Invalid params: 'name' field required", requestId);
            }

            var arguments = paramsObj["arguments"] as JObject ?? new JObject();

            this.Context.Logger?.LogInformation($"Getting prompt: {promptName}");

            // Check if prompt exists in defined prompts
            var definedPrompts = GetDefinedPrompts();
            var promptDefinition = definedPrompts.FirstOrDefault(p => p["name"]?.ToString() == promptName);
            
            if (promptDefinition == null)
            {
                return CreateErrorResponse(-32602, $"Unknown prompt: {promptName}", requestId);
            }

            // Build the prompt dynamically by calling the corresponding builder method
            // When you add a prompt to GetDefinedPrompts(), implement a builder method:
            // private (JArray messages, string description) Build{PromptName}Prompt(JObject arguments)
            // Then call it here based on promptName (similar to ExecuteToolByName pattern)
            
            throw new NotImplementedException($"Prompt builder not implemented for '{promptName}'. Add a Build{promptName.Replace("_", "")}Prompt method.");

            // Unreachable code removed - return statement moved into builder implementations
        }
        catch (ArgumentException ex)
        {
            this.Context.Logger?.LogError($"Invalid prompt parameter: {ex.Message}");
            return CreateErrorResponse(-32602, $"Invalid params: {ex.Message}", requestId);
        }
        catch (Exception ex)
        {
            this.Context.Logger?.LogError($"Prompt get error: {ex.Message}");
            return CreateErrorResponse(-32000, $"Prompt retrieval failed: {ex.Message}", requestId);
        }
    }

    /// <summary>
    /// Handles the completion/complete request for prompt argument completion
    /// </summary>
    private async Task<HttpResponseMessage> HandleCompletionComplete(JObject requestJson, JToken requestId)
    {
        this.Context.Logger?.LogInformation("Handling completion/complete request");

        try
        {
            var paramsObj = requestJson["params"] as JObject;
            if (paramsObj == null)
            {
                return CreateErrorResponse(-32602, "Invalid params: 'params' object required", requestId);
            }

            var refObj = paramsObj["ref"] as JObject;
            if (refObj == null)
            {
                return CreateErrorResponse(-32602, "Invalid params: 'ref' object required", requestId);
            }

            var refType = refObj["type"]?.ToString();
            var argumentName = paramsObj["argument"]?["name"]?.ToString();
            var argumentValue = paramsObj["argument"]?["value"]?.ToString() ?? "";

            this.Context.Logger?.LogInformation($"Completing {refType} argument: {argumentName} = {argumentValue}");

            // Only support prompt completions (resources don't have parameters)
            JArray completions;

            if (refType == "ref/prompt")
            {
                var promptName = refObj["name"]?.ToString();
                completions = GetPromptArgumentCompletions(promptName, argumentName, argumentValue);
            }
            else
            {
                return CreateErrorResponse(-32602, $"Unsupported ref type: {refType}. Only prompt completions are supported.", requestId);
            }

            var result = new JObject
            {
                ["completion"] = new JObject
                {
                    ["values"] = completions,
                    ["total"] = completions.Count,
                    ["hasMore"] = false
                }
            };

            return CreateSuccessResponse(result, requestId);
        }
        catch (Exception ex)
        {
            this.Context.Logger?.LogError($"Completion error: {ex.Message}");
            return CreateErrorResponse(-32000, $"Completion failed: {ex.Message}", requestId);
        }
    }

    /// <summary>
    /// Executes the get_weather tool
    /// Maps to an external weather API
    /// </summary>
    private async Task<JObject> ExecuteGetWeatherTool(JObject arguments)
    {
        try
        {
            // TODO: Implement external API call logic
            // 1. Extract parameters from arguments
            //    var location = arguments?["location"]?.ToString();
            //    var units = arguments?["units"]?.ToString() ?? "metric";
            //
            // 2. Validate required parameters
            //    if (string.IsNullOrEmpty(location))
            //        throw new ArgumentException("location is required");
            //    
            //    // Validate units if provided
            //    if (!string.IsNullOrEmpty(units) && !new[] { "metric", "imperial", "standard" }.Contains(units))
            //        throw new ArgumentException($"Invalid units '{units}'. Must be 'metric', 'imperial', or 'standard'");
            //
            // 3. Build external API request
            //    var apiUrl = $"https://api.weather.com/v1/current?location={Uri.EscapeDataString(location)}&units={units}";
            //    var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
            //    request.Headers.Add("X-API-Key", "your-api-key");
            //
            // 4. Call external API with cancellation support
            //    var response = await this.Context.SendAsync(request, this.CancellationToken).ConfigureAwait(false);
            //
            //    if (!response.IsSuccessStatusCode)
            //    {
            //        var errorBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            //        throw new HttpRequestException($"Weather API error: {response.StatusCode} - {errorBody}");
            //    }
            //
            // 5. Parse and transform response
            //    var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            //    JObject weatherData;
            //    try
            //    {
            //        weatherData = JObject.Parse(responseBody);
            //    }
            //    catch (JsonException ex)
            //    {
            //        throw new JsonException($"Failed to parse weather API response: {ex.Message}", ex);
            //    }
            //
            // 6. Return formatted result
            //    return new JObject
            //    {
            //        ["location"] = location,
            //        ["temperature"] = weatherData["temp"],
            //        ["conditions"] = weatherData["description"],
            //        ["units"] = units,
            //        ["timestamp"] = DateTime.UtcNow.ToString("o")
            //    };

            // Placeholder implementation
            this.Context.Logger?.LogInformation("get_weather tool called (placeholder implementation)");
            
            return new JObject
            {
                ["message"] = "Tool implementation placeholder - replace with actual API call",
                ["tool"] = "get_weather",
                ["arguments"] = arguments ?? new JObject()
            };
        }
        catch (ArgumentException)
        {
            // Re-throw validation errors to be caught by HandleToolsCall
            throw;
        }
        catch (HttpRequestException)
        {
            // Re-throw network errors to be caught by HandleToolsCall
            throw;
        }
        catch (JsonException)
        {
            // Re-throw JSON errors to be caught by HandleToolsCall
            throw;
        }
        catch (Exception ex)
        {
            // Log unexpected errors and re-throw
            this.Context.Logger?.LogError($"Unexpected error in get_weather tool: {ex.Message}\n{ex.StackTrace}");
            throw new Exception($"Failed to execute get_weather tool: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Executes the search_data tool
    /// Maps to an external search API
    /// </summary>
    private async Task<JObject> ExecuteSearchDataTool(JObject arguments)
    {
        try
        {
            // TODO: Implement external API call logic
            // 1. Extract parameters from arguments
            //    var query = arguments?["query"]?.ToString();
            //    var limit = arguments?["limit"]?.Value<int>() ?? 10;
            //
            // 2. Validate required parameters
            //    if (string.IsNullOrEmpty(query))
            //        throw new ArgumentException("query is required");
            //    
            //    if (limit < 1 || limit > 100)
            //        throw new ArgumentException($"Invalid limit {limit}. Must be between 1 and 100");
            //
            // 3. Build external API request
            //    var apiUrl = $"https://api.example.com/search?q={Uri.EscapeDataString(query)}&limit={limit}";
            //    var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
            //    request.Headers.Add("Authorization", "Bearer your-token");
            //
            // 4. Call external API with cancellation support
            //    var response = await this.Context.SendAsync(request, this.CancellationToken).ConfigureAwait(false);
            //
            //    if (!response.IsSuccessStatusCode)
            //    {
            //        var errorBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            //        throw new HttpRequestException($"Search API error: {response.StatusCode} - {errorBody}");
            //    }
            //
            // 5. Parse and transform response
            //    var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            //    JArray searchResults;
            //    try
            //    {
            //        searchResults = JArray.Parse(responseBody);
            //    }
            //    catch (JsonException ex)
            //    {
            //        throw new JsonException($"Failed to parse search API response: {ex.Message}", ex);
            //    }
            //
            // 6. Return formatted result
            //    return new JObject
            //    {
            //        ["query"] = query,
            //        ["results"] = searchResults,
            //        ["count"] = searchResults.Count,
            //        ["limit"] = limit,
            //        ["timestamp"] = DateTime.UtcNow.ToString("o")
            //    };

            // Placeholder implementation
            this.Context.Logger?.LogInformation("search_data tool called (placeholder implementation)");
            
            return new JObject
            {
                ["message"] = "Tool implementation placeholder - replace with actual API call",
                ["tool"] = "search_data",
                ["arguments"] = arguments ?? new JObject()
            };
        }
        catch (ArgumentException)
        {
            // Re-throw validation errors to be caught by HandleToolsCall
            throw;
        }
        catch (HttpRequestException)
        {
            // Re-throw network errors to be caught by HandleToolsCall
            throw;
        }
        catch (JsonException)
        {
            // Re-throw JSON errors to be caught by HandleToolsCall
            throw;
        }
        catch (Exception ex)
        {
            // Log unexpected errors and re-throw
            this.Context.Logger?.LogError($"Unexpected error in search_data tool: {ex.Message}\n{ex.StackTrace}");
            throw new Exception($"Failed to execute search_data tool: {ex.Message}", ex);
        }
    }

    // Add more tool execution methods here following the same pattern



    // Add prompt building methods here when you define prompts in GetDefinedPrompts()
    // Example pattern:
    // private (JArray messages, string description) BuildYourPromptNamePrompt(JObject arguments)
    // {
    //     // 1. Extract and validate arguments
    //     var param = arguments["param_name"]?.ToString();
    //     if (string.IsNullOrEmpty(param))
    //         throw new ArgumentException("param_name is required");
    //
    //     // 2. Build prompt messages
    //     var messages = new JArray
    //     {
    //         new JObject
    //         {
    //             ["role"] = "user",
    //             ["content"] = new JObject
    //             {
    //                 ["type"] = "text",
    //                 ["text"] = $"Your prompt text with {param}"
    //             }
    //         }
    //     };
    //
    //     // 3. Return messages and description
    //     return (messages, $"Description for {param}");
    // }

    /// <summary>
    /// Gets argument completion values for prompts
    /// Implement completion logic when you add prompts to GetDefinedPrompts()
    /// </summary>
    private JArray GetPromptArgumentCompletions(string promptName, string argName, string partialValue)
    {
        // TODO: Implement prompt argument completion logic when prompts are defined
        // Example pattern:
        // if (promptName == "your_prompt" && argName == "your_argument")
        // {
        //     var validValues = new[] { "option1", "option2", "option3" };
        //     var filtered = validValues.Where(v => v.StartsWith(partialValue, StringComparison.OrdinalIgnoreCase));
        //     
        //     var completions = new JArray();
        //     foreach (var value in filtered)
        //     {
        //         completions.Add(new JObject
        //         {
        //             ["value"] = value,
        //             ["label"] = value.ToUpper() // Display label
        //         });
        //     }
        //     return completions;
        // }

        // Return empty array when no prompts defined
        return new JArray();
    }

    /// <summary>
    /// Creates a successful JSON-RPC 2.0 response
    /// </summary>
    private HttpResponseMessage CreateSuccessResponse(JObject result, JToken id)
    {
        var responseJson = new JObject
        {
            ["jsonrpc"] = "2.0",
            ["result"] = result,
            ["id"] = id
        };

        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Content = CreateJsonContent(responseJson.ToString());
        return response;
    }

    /// <summary>
    /// Creates a JSON-RPC 2.0 error response
    /// 
    /// Standard JSON-RPC 2.0 Error Codes:
    /// -32700: Parse error - Invalid JSON was received
    /// -32600: Invalid Request - The JSON sent is not a valid Request object
    /// -32601: Method not found - The method does not exist
    /// -32602: Invalid params - Invalid method parameter(s)
    /// -32603: Internal error - Internal JSON-RPC error
    /// -32000 to -32099: Server error - Reserved for implementation-defined server-errors
    /// </summary>
    private HttpResponseMessage CreateErrorResponse(int code, string message, JToken id, JObject data = null)
    {
        this.Context.Logger?.LogWarning($"Creating error response: [{code}] {message}");

        var errorResponse = new JObject
        {
            ["jsonrpc"] = "2.0",
            ["error"] = new JObject
            {
                ["code"] = code,
                ["message"] = message
            },
            ["id"] = id
        };

        if (data != null)
        {
            errorResponse["error"]["data"] = data;
        }

        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Content = CreateJsonContent(errorResponse.ToString());
        return response;
    }

    /// <summary>
    /// Creates an agent error response with categorized error types
    /// </summary>
    private HttpResponseMessage CreateAgentErrorResponse(string message, string details, string errorType = "AgentError", string errorCode = null)
    {
        this.Context.Logger?.LogError($"Agent error ({errorType}): {message}");

        var errorResponse = new JObject
        {
            ["response"] = null,
            ["error"] = message,
            ["errorType"] = errorType,
            ["details"] = details,
            ["mode"] = null
        };

        if (!string.IsNullOrEmpty(errorCode))
        {
            errorResponse["errorCode"] = errorCode;
        }

        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Content = CreateJsonContent(errorResponse.ToString());
        return response;
    }

    private HttpResponseMessage ValidateJsonRpcRequest(JObject request)
    {
        // Check jsonrpc version
        if (!request.ContainsKey("jsonrpc") || request["jsonrpc"]?.ToString() != "2.0")
        {
            return CreateErrorResponse(-32600, "Invalid Request: jsonrpc field must be '2.0'", request["id"]);
        }

        // Check method exists and is a string
        if (!request.ContainsKey("method") || request["method"]?.Type != JTokenType.String)
        {
            return CreateErrorResponse(-32600, "Invalid Request: method field is required and must be a string", request["id"]);
        }

        string method = request["method"].ToString();
        if (string.IsNullOrWhiteSpace(method))
        {
            return CreateErrorResponse(-32600, "Invalid Request: method cannot be empty", request["id"]);
        }

        // Params is optional, but if present must be object or array
        if (request.ContainsKey("params"))
        {
            var paramsType = request["params"]?.Type;
            if (paramsType != JTokenType.Object && paramsType != JTokenType.Array && paramsType != JTokenType.Null)
            {
                return CreateErrorResponse(-32600, "Invalid Request: params must be an object or array", request["id"]);
            }
        }

        return null;
    }
}