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
    // Configure your preferred AI model provider here
    private const string DEFAULT_BASE_URL = "https://YOUR_RESOURCE_NAME.openai.azure.com"; // Azure OpenAI
    
    private const string DEFAULT_MODEL = "gpt-4o"; // Azure OpenAI GPT-4o
    
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
            You are an intelligent AI assistant powered by advanced language models (Claude or GPT) with access to Microsoft Learn documentation through the Model Context Protocol (MCP).

            Your Purpose:
            - Help users understand and implement Microsoft technologies by searching and citing official Microsoft Learn documentation
            - Provide accurate, up-to-date technical guidance grounded in Microsoft's official documentation
            - Guide developers through Azure, Power Platform, .NET, and other Microsoft technology stacks

            Your Behavior:
            - Search Microsoft Learn documentation using microsoft_docs_search when users ask about Microsoft technologies
            - Search for code samples using microsoft_code_sample_search when users need implementation examples or code snippets
            - Fetch complete documentation pages using microsoft_docs_fetch when detailed examples, tutorials, or code samples are needed
            - Always cite your sources with URLs from Microsoft Learn
            - If you're not certain about something, search the documentation rather than speculating
            - Provide step-by-step guidance when appropriate, referencing official documentation
            - Synthesize information from multiple documentation sources when needed to provide comprehensive answers

            Response Style:
            - Be clear, concise, and technically accurate
            - Structure responses with clear sections when appropriate (Overview, Steps, Code Examples, etc.)
            - Include direct quotes from documentation when they provide critical details
            - Always provide Microsoft Learn URLs for further reading
            - Use code blocks with appropriate language tags for code samples
            - Be professional yet approachable in tone

            Available Tools:
            - microsoft_docs_search: Search Microsoft Learn for relevant documentation (returns summaries and URLs)
            - microsoft_code_sample_search: Search for code samples and examples with optional language filter
            - microsoft_docs_fetch: Retrieve complete documentation pages (use after search for detailed content)

            When to Use Which Tool:
            - Use microsoft_docs_search first to find relevant documentation URLs
            - Use microsoft_code_sample_search when users need implementation examples or code snippets (optionally specify language)
            - Use microsoft_docs_fetch when you need complete tutorials, detailed explanations, or full page content
            - Search multiple times with different queries if initial results don't answer the question
            - Fetch multiple pages if the topic requires information from several sources";
    }

    /// <summary>
    /// Gets the server information returned during initialization
    /// </summary>
    private JObject GetServerInfo()
    {
        return new JObject
        {
            ["name"] = "microsoft-learn-mcp-server",
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
            new JObject
            {
                ["name"] = "microsoft_docs_search",
                ["description"] = "Search official Microsoft Learn documentation for relevant technical content. Returns up to 10 high-quality content chunks with title, URL, and excerpts. Use this to quickly ground answers in Microsoft's official documentation.",
                ["inputSchema"] = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["query"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Search query for Microsoft Learn documentation (e.g., 'Azure OpenAI chat completions', 'Power Platform custom connectors')"
                        }
                    },
                    ["required"] = new JArray { "query" }
                }
            },
            new JObject
            {
                ["name"] = "microsoft_docs_fetch",
                ["description"] = "Fetch complete Microsoft Learn documentation page content in markdown format. Use this after microsoft_docs_search when you need detailed tutorials, code samples, or complete documentation. Requires a valid Microsoft Learn URL.",
                ["inputSchema"] = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["url"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Full URL to Microsoft Learn documentation page (must start with https://learn.microsoft.com/)"
                        }
                    },
                    ["required"] = new JArray { "url" }
                }
            },
            new JObject
            {
                ["name"] = "microsoft_code_sample_search",
                ["description"] = "Search for code samples and examples in official Microsoft Learn documentation. Returns relevant code snippets from Microsoft documentation with practical implementation examples. Use when you need code examples for Microsoft/Azure products, services, or SDKs.",
                ["inputSchema"] = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["query"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Search query for code samples (e.g., 'Azure OpenAI chat completion C#', 'Power Automate custom connector authentication')"
                        },
                        ["language"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Optional programming language filter (e.g., 'csharp', 'javascript', 'python', 'typescript', 'powershell')"
                        }
                    },
                    ["required"] = new JArray { "query" }
                }
            }
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

            // Use developer-configured defaults
            var model = DEFAULT_MODEL;
            var baseUrl = DEFAULT_BASE_URL;

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
    /// Executes AI API call with function calling support
    /// Detects provider based on baseUrl and uses appropriate API format
    /// Supports: Anthropic Claude, OpenAI, Azure OpenAI
    /// </summary>
    private async Task<(string response, JArray toolCalls, int tokensUsed)> ExecuteAIWithFunctions(
        string apiKey, string model, string baseUrl, JArray messages, JArray functions,
        double temperature, int maxTokens, bool autoExecute, int maxIterations)
    {
        // Detect AI provider based on baseUrl
        var isAnthropic = baseUrl.IndexOf("anthropic.com", StringComparison.OrdinalIgnoreCase) >= 0;
        var isAzureOpenAI = baseUrl.IndexOf(".openai.azure.com", StringComparison.OrdinalIgnoreCase) >= 0;
        var isOpenAI = baseUrl.IndexOf("api.openai.com", StringComparison.OrdinalIgnoreCase) >= 0 || (!isAnthropic && !isAzureOpenAI);

        this.Context.Logger?.LogInformation($"Detected provider - Anthropic: {isAnthropic}, Azure OpenAI: {isAzureOpenAI}, OpenAI: {isOpenAI}");

        var allToolCalls = new JArray();
        var totalTokens = 0;
        var iterations = 0;

        while (iterations < maxIterations)
        {
            iterations++;
            this.Context.Logger?.LogInformation($"AI API call iteration {iterations}");

            HttpRequestMessage request;
            JObject requestBody;

            if (isAnthropic)
            {
                // Anthropic Claude format
                requestBody = new JObject
                {
                    ["model"] = model,
                    ["max_tokens"] = maxTokens,
                    ["temperature"] = temperature,
                    ["messages"] = ConvertToAnthropicMessages(messages)
                };

                if (functions != null && functions.Count > 0)
                {
                    requestBody["tools"] = ConvertToAnthropicTools(functions);
                }

                // Extract system message for Anthropic
                var systemMsg = messages.FirstOrDefault(m => m["role"]?.ToString() == "system");
                if (systemMsg != null)
                {
                    requestBody["system"] = systemMsg["content"]?.ToString();
                }

                request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v1/messages");
                request.Headers.Add("x-api-key", apiKey);
                request.Headers.Add("anthropic-version", "2023-06-01");
            }
            else if (isAzureOpenAI)
            {
                // Azure OpenAI format
                requestBody = new JObject
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

                var apiVersion = "2024-08-01-preview";
                request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/openai/deployments/{model}/chat/completions?api-version={apiVersion}");
                request.Headers.Add("api-key", apiKey);
            }
            else
            {
                // OpenAI format
                requestBody = new JObject
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

                request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
                request.Headers.Add("Authorization", $"Bearer {apiKey}");
            }

            request.Content = new StringContent(requestBody.ToString(), System.Text.Encoding.UTF8, "application/json");

            var response = await this.Context.SendAsync(request, this.CancellationToken).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                this.Context.Logger?.LogError($"AI API error: {responseBody}");
                throw new Exception($"AI API error: {response.StatusCode} - {responseBody}");
            }

            var responseJson = JObject.Parse(responseBody);

            // Parse response based on provider
            string content;
            JArray toolCalls;
            int tokensThisCall;

            if (isAnthropic)
            {
                // Anthropic response format
                var usage = responseJson["usage"];
                tokensThisCall = (usage?["input_tokens"]?.Value<int>() ?? 0) + (usage?["output_tokens"]?.Value<int>() ?? 0);
                totalTokens += tokensThisCall;

                content = "";
                toolCalls = new JArray();

                var contentArray = responseJson["content"] as JArray;
                if (contentArray != null)
                {
                    foreach (var block in contentArray)
                    {
                        var blockType = block["type"]?.ToString();
                        if (blockType == "text")
                        {
                            content += block["text"]?.ToString();
                        }
                        else if (blockType == "tool_use")
                        {
                            toolCalls.Add(new JObject
                            {
                                ["id"] = block["id"],
                                ["type"] = "function",
                                ["function"] = new JObject
                                {
                                    ["name"] = block["name"],
                                    ["arguments"] = block["input"]?.ToString()
                                }
                            });
                        }
                    }
                }

                // If no tool calls, return content
                if (toolCalls.Count == 0)
                {
                    return (content, allToolCalls, totalTokens);
                }
            }
            else
            {
                // OpenAI/Azure OpenAI response format
                var usage = responseJson["usage"];
                totalTokens += usage?["total_tokens"]?.Value<int>() ?? 0;

                var choice = responseJson["choices"]?[0];
                var message = choice?["message"] as JObject;
                toolCalls = message?["tool_calls"] as JArray;

                // If no tool calls, return the response
                if (toolCalls == null || toolCalls.Count == 0)
                {
                    content = message?["content"]?.ToString() ?? "";
                    return (content, allToolCalls, totalTokens);
                }
            }

            // Execute tool calls if auto-execute is enabled
            if (!autoExecute)
            {
                return ("Tool calls ready but auto-execute is disabled", allToolCalls, totalTokens);
            }

            // Add assistant message with tool calls to conversation (format depends on provider)
            if (isAnthropic)
            {
                // For Anthropic, add assistant message with tool_use blocks
                var assistantContent = new JArray();
                foreach (var toolCall in toolCalls)
                {
                    var function = toolCall["function"] as JObject;
                    assistantContent.Add(new JObject
                    {
                        ["type"] = "tool_use",
                        ["id"] = toolCall["id"],
                        ["name"] = function["name"],
                        ["input"] = JObject.Parse(function["arguments"]?.ToString() ?? "{}")
                    });
                }
                messages.Add(new JObject
                {
                    ["role"] = "assistant",
                    ["content"] = assistantContent
                });
            }
            else
            {
                // For OpenAI/Azure OpenAI, add message with tool_calls
                messages.Add(new JObject
                {
                    ["role"] = "assistant",
                    ["tool_calls"] = toolCalls,
                    ["content"] = null
                });
            }

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

                // Add tool result to conversation (format depends on provider)
                if (isAnthropic)
                {
                    // Anthropic format: user message with tool_result
                    messages.Add(new JObject
                    {
                        ["role"] = "user",
                        ["content"] = new JArray
                        {
                            new JObject
                            {
                                ["type"] = "tool_result",
                                ["tool_use_id"] = toolCallId,
                                ["content"] = toolResult.ToString()
                            }
                        }
                    });
                }
                else
                {
                    // OpenAI/Azure OpenAI format: tool message
                    messages.Add(new JObject
                    {
                        ["role"] = "tool",
                        ["tool_call_id"] = toolCallId,
                        ["content"] = toolResult.ToString()
                    });
                }
            }

            // Continue loop to get AI's response after tool execution
        }

        // Max iterations reached
        return ("Max tool call iterations reached", allToolCalls, totalTokens);
    }

    /// <summary>
    /// Converts OpenAI-format messages to Anthropic format (removes system messages, they go in separate field)
    /// </summary>
    private JArray ConvertToAnthropicMessages(JArray messages)
    {
        var anthropicMessages = new JArray();
        foreach (var msg in messages)
        {
            var role = msg["role"]?.ToString();
            if (role != "system") // System messages handled separately in Anthropic
            {
                anthropicMessages.Add(msg);
            }
        }
        return anthropicMessages;
    }

    /// <summary>
    /// Converts OpenAI-format tools to Anthropic format
    /// </summary>
    private JArray ConvertToAnthropicTools(JArray openAiTools)
    {
        var anthropicTools = new JArray();
        foreach (var tool in openAiTools)
        {
            var function = tool["function"] as JObject;
            if (function != null)
            {
                anthropicTools.Add(new JObject
                {
                    ["name"] = function["name"],
                    ["description"] = function["description"],
                    ["input_schema"] = function["parameters"]
                });
            }
        }
        return anthropicTools;
    }

    /// <summary>
    /// Executes AI API call without function calling (pure generation)
    /// Supports: Anthropic Claude, OpenAI, Azure OpenAI
    /// </summary>
    private async Task<(string response, int tokensUsed)> ExecuteAICompletion(
        string apiKey, string model, string baseUrl, JArray messages, double temperature, int maxTokens)
    {
        // Detect AI provider based on baseUrl
        var isAnthropic = baseUrl.IndexOf("anthropic.com", StringComparison.OrdinalIgnoreCase) >= 0;
        var isAzureOpenAI = baseUrl.IndexOf(".openai.azure.com", StringComparison.OrdinalIgnoreCase) >= 0;

        HttpRequestMessage request;
        JObject requestBody;

        if (isAnthropic)
        {
            // Anthropic Claude format
            requestBody = new JObject
            {
                ["model"] = model,
                ["max_tokens"] = maxTokens,
                ["temperature"] = temperature,
                ["messages"] = ConvertToAnthropicMessages(messages)
            };

            // Extract system message for Anthropic
            var systemMsg = messages.FirstOrDefault(m => m["role"]?.ToString() == "system");
            if (systemMsg != null)
            {
                requestBody["system"] = systemMsg["content"]?.ToString();
            }

            request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v1/messages");
            request.Headers.Add("x-api-key", apiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");
        }
        else if (isAzureOpenAI)
        {
            // Azure OpenAI format
            requestBody = new JObject
            {
                ["model"] = model,
                ["messages"] = messages,
                ["temperature"] = temperature,
                ["max_tokens"] = maxTokens
            };

            var apiVersion = "2024-08-01-preview";
            request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/openai/deployments/{model}/chat/completions?api-version={apiVersion}");
            request.Headers.Add("api-key", apiKey);
        }
        else
        {
            // OpenAI format
            requestBody = new JObject
            {
                ["model"] = model,
                ["messages"] = messages,
                ["temperature"] = temperature,
                ["max_tokens"] = maxTokens
            };

            request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
            request.Headers.Add("Authorization", $"Bearer {apiKey}");
        }

        request.Content = new StringContent(requestBody.ToString(), System.Text.Encoding.UTF8, "application/json");

        var response = await this.Context.SendAsync(request, this.CancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            this.Context.Logger?.LogError($"AI API error: {responseBody}");
            throw new Exception($"AI API error: {response.StatusCode} - {responseBody}");
        }

        var responseJson = JObject.Parse(responseBody);
        int totalTokens;
        string content;

        if (isAnthropic)
        {
            // Anthropic response format
            var usage = responseJson["usage"];
            totalTokens = (usage?["input_tokens"]?.Value<int>() ?? 0) + (usage?["output_tokens"]?.Value<int>() ?? 0);
            
            var contentArray = responseJson["content"] as JArray;
            content = "";
            if (contentArray != null)
            {
                foreach (var block in contentArray)
                {
                    if (block["type"]?.ToString() == "text")
                    {
                        content += block["text"]?.ToString();
                    }
                }
            }
        }
        else
        {
            // OpenAI/Azure OpenAI response format
            var usage = responseJson["usage"];
            totalTokens = usage?["total_tokens"]?.Value<int>() ?? 0;
            content = responseJson["choices"]?[0]?["message"]?["content"]?.ToString() ?? "";
        }

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
            case "microsoft_docs_search":
                return await ExecuteMicrosoftDocsSearchTool(arguments).ConfigureAwait(false);

            case "microsoft_docs_fetch":
                return await ExecuteMicrosoftDocsFetchTool(arguments).ConfigureAwait(false);

            case "microsoft_code_sample_search":
                return await ExecuteMicrosoftCodeSampleSearchTool(arguments).ConfigureAwait(false);

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
    /// Executes the microsoft_docs_search tool
    /// Searches Microsoft Learn documentation for relevant content
    /// </summary>
    private async Task<JObject> ExecuteMicrosoftDocsSearchTool(JObject arguments)
    {
        try
        {
            // Extract and validate parameters
            var query = arguments?["query"]?.ToString();
            if (string.IsNullOrEmpty(query))
                throw new ArgumentException("query parameter is required");

            this.Context.Logger?.LogInformation($"Searching Microsoft Learn for: {query}");

            // Call Learn MCP server with microsoft_docs_search tool
            var mcpRequest = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 1,
                ["method"] = "tools/call",
                ["params"] = new JObject
                {
                    ["name"] = "microsoft_docs_search",
                    ["arguments"] = new JObject
                    {
                        ["query"] = query
                    }
                }
            };

            var result = await CallLearnMCPServer(mcpRequest).ConfigureAwait(false);
            
            // Extract content from MCP response
            var content = result["result"]?["content"];
            if (content != null)
            {
                // Extract source URLs from the results for better attribution
                var sourceUrls = new JArray();
                foreach (var item in content)
                {
                    if (item["type"]?.ToString() == "text")
                    {
                        var text = item["text"]?.ToString();
                        if (!string.IsNullOrEmpty(text))
                        {
                            try
                            {
                                var resultsArray = JArray.Parse(text);
                                foreach (var doc in resultsArray)
                                {
                                    var url = doc["contentUrl"]?.ToString();
                                    var title = doc["title"]?.ToString();
                                    if (!string.IsNullOrEmpty(url))
                                    {
                                        sourceUrls.Add(new JObject
                                        {
                                            ["url"] = url,
                                            ["title"] = title ?? "Untitled"
                                        });
                                    }
                                }
                            }
                            catch { /* If parsing fails, continue without source URLs */ }
                        }
                    }
                }
                
                // Return the search results with source attribution
                return new JObject
                {
                    ["query"] = query,
                    ["results"] = content,
                    ["sources"] = sourceUrls,
                    ["sourceCount"] = sourceUrls.Count
                };
            }
            
            // If no content, return empty results
            return new JObject
            {
                ["query"] = query,
                ["results"] = new JArray(),
                ["sources"] = new JArray(),
                ["message"] = "No results found"
            };
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            this.Context.Logger?.LogError($"Learn MCP network error: {ex.Message}");
            throw new HttpRequestException($"Failed to connect to Microsoft Learn MCP server: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            this.Context.Logger?.LogError($"Unexpected error in microsoft_docs_search: {ex.Message}\n{ex.StackTrace}");
            throw new Exception($"Microsoft Learn search failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Executes the microsoft_docs_fetch tool
    /// Fetches complete Microsoft Learn documentation page
    /// </summary>
    private async Task<JObject> ExecuteMicrosoftDocsFetchTool(JObject arguments)
    {
        try
        {
            // Extract and validate parameters
            var url = arguments?["url"]?.ToString();
            if (string.IsNullOrEmpty(url))
                throw new ArgumentException("url parameter is required");

            // Validate URL is from Microsoft Learn
            if (!url.StartsWith("https://learn.microsoft.com/", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("URL must be from Microsoft Learn (https://learn.microsoft.com/)");

            this.Context.Logger?.LogInformation($"Fetching Microsoft Learn page: {url}");

            // Call Learn MCP server with microsoft_docs_fetch tool
            var mcpRequest = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 1,
                ["method"] = "tools/call",
                ["params"] = new JObject
                {
                    ["name"] = "microsoft_docs_fetch",
                    ["arguments"] = new JObject
                    {
                        ["url"] = url
                    }
                }
            };

            var result = await CallLearnMCPServer(mcpRequest).ConfigureAwait(false);
            
            // Extract content from MCP response
            var content = result["result"]?["content"];
            if (content != null)
            {
                // Try to extract page title from the markdown content
                var pageTitle = "Microsoft Learn Documentation";
                foreach (var item in content)
                {
                    if (item["type"]?.ToString() == "text")
                    {
                        var text = item["text"]?.ToString();
                        if (!string.IsNullOrEmpty(text))
                        {
                            // Try to extract title from first markdown heading
                            var lines = text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var line in lines)
                            {
                                if (line.StartsWith("# "))
                                {
                                    pageTitle = line.Substring(2).Trim();
                                    break;
                                }
                            }
                        }
                    }
                }
                
                // Return the documentation content with proper source attribution
                return new JObject
                {
                    ["url"] = url,
                    ["title"] = pageTitle,
                    ["content"] = content,
                    ["source"] = new JObject
                    {
                        ["url"] = url,
                        ["title"] = pageTitle,
                        ["provider"] = "Microsoft Learn"
                    }
                };
            }
            
            // If no content, return error
            throw new Exception("No content received from Microsoft Learn");
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            this.Context.Logger?.LogError($"Learn MCP network error: {ex.Message}");
            throw new HttpRequestException($"Failed to connect to Microsoft Learn MCP server: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            this.Context.Logger?.LogError($"Unexpected error in microsoft_docs_fetch: {ex.Message}\n{ex.StackTrace}");
            throw new Exception($"Microsoft Learn fetch failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Executes the microsoft_code_sample_search tool
    /// Searches Microsoft Learn for relevant code samples
    /// </summary>
    private async Task<JObject> ExecuteMicrosoftCodeSampleSearchTool(JObject arguments)
    {
        try
        {
            // Extract and validate parameters
            var query = arguments?["query"]?.ToString();
            if (string.IsNullOrEmpty(query))
                throw new ArgumentException("query parameter is required");

            var language = arguments?["language"]?.ToString();

            this.Context.Logger?.LogInformation($"Searching Microsoft Learn code samples for: {query}" + 
                (string.IsNullOrEmpty(language) ? "" : $" (language: {language})"));

            // Call Learn MCP server with microsoft_code_sample_search tool
            var mcpParams = new JObject
            {
                ["name"] = "microsoft_code_sample_search",
                ["arguments"] = new JObject
                {
                    ["query"] = query
                }
            };

            // Add optional language parameter if provided
            if (!string.IsNullOrEmpty(language))
            {
                ((JObject)mcpParams["arguments"])["language"] = language;
            }

            var mcpRequest = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 1,
                ["method"] = "tools/call",
                ["params"] = mcpParams
            };

            var result = await CallLearnMCPServer(mcpRequest).ConfigureAwait(false);
            
            // Extract content from MCP response
            var content = result["result"]?["content"];
            if (content != null)
            {
                // Extract source URLs from the code sample results
                var sourceUrls = new JArray();
                foreach (var item in content)
                {
                    if (item["type"]?.ToString() == "text")
                    {
                        var text = item["text"]?.ToString();
                        if (!string.IsNullOrEmpty(text))
                        {
                            try
                            {
                                var samplesArray = JArray.Parse(text);
                                foreach (var sample in samplesArray)
                                {
                                    var url = sample["url"]?.ToString();
                                    var title = sample["title"]?.ToString();
                                    if (!string.IsNullOrEmpty(url))
                                    {
                                        sourceUrls.Add(new JObject
                                        {
                                            ["url"] = url,
                                            ["title"] = title ?? "Code Sample"
                                        });
                                    }
                                }
                            }
                            catch { /* If parsing fails, continue without source URLs */ }
                        }
                    }
                }
                
                // Return the search results with source attribution
                return new JObject
                {
                    ["query"] = query,
                    ["language"] = language ?? "all",
                    ["results"] = content,
                    ["sources"] = sourceUrls,
                    ["sourceCount"] = sourceUrls.Count
                };
            }
            
            // If no content, return empty results
            return new JObject
            {
                ["query"] = query,
                ["language"] = language ?? "all",
                ["results"] = new JArray(),
                ["sources"] = new JArray()
            };
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            this.Context.Logger?.LogError($"Learn MCP network error: {ex.Message}");
            throw new HttpRequestException($"Failed to connect to Microsoft Learn MCP server: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            this.Context.Logger?.LogError($"Unexpected error in microsoft_code_sample_search: {ex.Message}\n{ex.StackTrace}");
            throw new Exception($"Microsoft Learn code sample search failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Helper method to call Learn MCP Server
    /// Handles communication with Microsoft Learn MCP endpoint
    /// </summary>
    private async Task<JObject> CallLearnMCPServer(JObject mcpRequest)
    {
        try
        {
            var learnMcpUrl = "https://learn.microsoft.com/api/mcp";
            
            // Create HTTP request
            var request = new HttpRequestMessage(HttpMethod.Post, learnMcpUrl);
            request.Content = new StringContent(mcpRequest.ToString(), System.Text.Encoding.UTF8, "application/json");
            
            // Learn MCP requires both application/json and text/event-stream Accept headers
            request.Headers.Accept.Clear();
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));
            
            // No authentication required for Learn MCP
            // Send request
            var response = await this.Context.SendAsync(request, this.CancellationToken).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            
            if (!response.IsSuccessStatusCode)
            {
                this.Context.Logger?.LogError($"Learn MCP error: {response.StatusCode} - {responseBody}");
                throw new HttpRequestException($"Learn MCP API error: {response.StatusCode} - {responseBody}");
            }
            
            // Check if response is Server-Sent Events (SSE) format
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
            if (contentType.IndexOf("text/event-stream") >= 0)
            {
                // Parse SSE format: extract JSON from data: lines
                var lines = responseBody.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                var jsonData = new StringBuilder();
                
                foreach (var line in lines)
                {
                    if (line.StartsWith("data: "))
                    {
                        var data = line.Substring(6).Trim();
                        if (data != "[DONE]" && !string.IsNullOrEmpty(data))
                        {
                            jsonData.Append(data);
                        }
                    }
                }
                
                if (jsonData.Length == 0)
                {
                    throw new Exception("No valid JSON data found in SSE response");
                }
                
                responseBody = jsonData.ToString();
            }
            
            // Parse and return response
            JObject responseJson;
            try
            {
                responseJson = JObject.Parse(responseBody);
            }
            catch (JsonException ex)
            {
                this.Context.Logger?.LogError($"Failed to parse response. Content-Type: {contentType}, Body: {responseBody}");
                throw new JsonException($"Failed to parse Learn MCP response: {ex.Message}", ex);
            }
            
            // Check for JSON-RPC errors
            if (responseJson.ContainsKey("error"))
            {
                var error = responseJson["error"];
                var errorMessage = error["message"]?.ToString() ?? "Unknown error";
                var errorCode = error["code"]?.Value<int>() ?? -32000;
                throw new Exception($"Learn MCP error [{errorCode}]: {errorMessage}");
            }
            
            return responseJson;
        }
        catch (JsonException)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            this.Context.Logger?.LogError($"Learn MCP call failed: {ex.Message}\n{ex.StackTrace}");
            throw;
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