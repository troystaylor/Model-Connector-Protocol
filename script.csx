using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// Model Context Protocol (MCP) Custom Code Handler
/// 
/// This script handles JSON-RPC 2.0 communication with MCP servers following the 
/// Model Context Protocol specification. It supports:
/// - Lifecycle management (initialize, initialized, ping)
/// - Server primitives (tools, resources, prompts)
/// - Client primitives (sampling, roots, completion)
/// - Real-time notifications
/// - Tool discovery caching for performance optimization
/// 
/// This template defines MCP tools in custom code that map to external APIs.
/// Tools are defined statically and exposed to Copilot Studio through the MCP protocol.
/// 
/// MCP Architecture:
/// - Data Layer: JSON-RPC 2.0 protocol for message exchange
/// - Transport Layer: HTTP POST for requests, SSE for streaming
/// 
/// Reference: https://modelcontextprotocol.io/docs/learn/architecture
/// </summary>
public class Script : ScriptBase
{
    // AI Orchestration Configuration
    private const string DEFAULT_MODEL = "gpt-4-turbo";
    private const string DEFAULT_BASE_URL = "https://api.openai.com/v1";
    private const int DEFAULT_MAX_TOKENS = 1000;
    private const double DEFAULT_TEMPERATURE = 0.7;
    private const int DEFAULT_MAX_TOOL_CALLS = 10;
    
    // Agent Modes
    private const string MODE_ORCHESTRATE = "orchestrate";
    private const string MODE_GENERATE = "generate";
    private const string MODE_EXECUTE = "execute";
    private const string MODE_CHAT = "chat";
    
    // Static cache for tool discovery results
    // Tools are static per MCP server and can be cached across requests
    private static readonly Dictionary<string, CachedToolList> _toolCache = new Dictionary<string, CachedToolList>();
    private static readonly object _cacheLock = new object();
    private static readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(30);

    // Cache entry for tool lists
    private class CachedToolList
    {
        public string ToolListJson { get; set; }
        public DateTime CachedAt { get; set; }
        public bool IsExpired => DateTime.UtcNow - CachedAt > _cacheExpiration;
    }

    #region Tool Definitions

    /// <summary>
    /// Defines the MCP tools available through this connector.
    /// Add your custom tools here that map to external API operations.
    /// </summary>
    private JArray GetDefinedTools()
    {
        return new JArray
        {
            // Example Tool 1: Get Weather
            new JObject
            {
                ["name"] = "get_weather",
                ["description"] = "Get current weather conditions for a location",
                ["inputSchema"] = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["location"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "City name or location to get weather for"
                        },
                        ["units"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Temperature units (metric or imperial)",
                            ["enum"] = new JArray { "metric", "imperial" },
                            ["default"] = "metric"
                        }
                    },
                    ["required"] = new JArray { "location" }
                }
            },

            // Example Tool 2: Search Data
            new JObject
            {
                ["name"] = "search_data",
                ["description"] = "Search for data using a query string",
                ["inputSchema"] = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["query"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Search query string"
                        },
                        ["limit"] = new JObject
                        {
                            ["type"] = "integer",
                            ["description"] = "Maximum number of results to return",
                            ["default"] = 10
                        }
                    },
                    ["required"] = new JArray { "query" }
                }
            }

            // Add more tools here following the same pattern
            // Each tool should have: name, description, and inputSchema
        };
    }

    /// <summary>
    /// Gets the server information returned during initialization
    /// </summary>
    private JObject GetServerInfo()
    {
        return new JObject
        {
            ["name"] = "mcp-custom-connector",
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
                ["subscribe"] = true, // Supports resource subscriptions
                ["listChanged"] = false // Set to true if resource list can change at runtime
            },
            ["prompts"] = new JObject
            {
                ["listChanged"] = false // Set to true if prompt list can change at runtime
            }
        };
    }

    #endregion

    #region Resource Definitions

    /// <summary>
    /// Defines the MCP resources available through this connector.
    /// Resources provide read-only access to data (files, databases, APIs, etc.)
    /// Add your custom resources here that map to external data sources.
    /// </summary>
    private JArray GetDefinedResources()
    {
        return new JArray
        {
            // Example Resource 1: Configuration File
            new JObject
            {
                ["uri"] = "config://settings.json",
                ["name"] = "Application Settings",
                ["description"] = "Current application configuration",
                ["mimeType"] = "application/json"
            },

            // Example Resource 2: Database Schema
            new JObject
            {
                ["uri"] = "db://schema/users",
                ["name"] = "User Database Schema",
                ["description"] = "Schema information for the users table",
                ["mimeType"] = "application/json"
            }

            // Add more resources here following the same pattern
            // Each resource should have: uri, name, description, mimeType
        };
    }

    /// <summary>
    /// Defines the MCP resource templates available through this connector.
    /// Resource templates are dynamic URIs with parameters for flexible queries.
    /// </summary>
    private JArray GetDefinedResourceTemplates()
    {
        return new JArray
        {
            // Example Template 1: User Profile
            new JObject
            {
                ["uriTemplate"] = "user://profile/{userId}",
                ["name"] = "user-profile",
                ["description"] = "Get user profile information by user ID",
                ["mimeType"] = "application/json"
            },

            // Example Template 2: Document Retrieval
            new JObject
            {
                ["uriTemplate"] = "docs://category/{category}/document/{docId}",
                ["name"] = "document-retrieval",
                ["description"] = "Retrieve documents by category and ID",
                ["mimeType"] = "text/markdown"
            }

            // Add more resource templates here
        };
    }

    #endregion

    #region Prompt Definitions

    /// <summary>
    /// Defines the MCP prompts available through this connector.
    /// Prompts are reusable templates with parameters that guide AI interactions.
    /// Add your custom prompts here that showcase best practices for using your connector.
    /// </summary>
    private JArray GetDefinedPrompts()
    {
        return new JArray
        {
            // Example Prompt 1: Data Analysis
            new JObject
            {
                ["name"] = "analyze_data",
                ["description"] = "Analyze data from a specific source with optional filters",
                ["arguments"] = new JArray
                {
                    new JObject
                    {
                        ["name"] = "source",
                        ["description"] = "Data source to analyze",
                        ["required"] = true
                    },
                    new JObject
                    {
                        ["name"] = "filters",
                        ["description"] = "Optional filters to apply to the data",
                        ["required"] = false
                    },
                    new JObject
                    {
                        ["name"] = "output_format",
                        ["description"] = "Desired output format (summary, detailed, chart)",
                        ["required"] = false
                    }
                }
            },

            // Example Prompt 2: Report Generation
            new JObject
            {
                ["name"] = "generate_report",
                ["description"] = "Generate a comprehensive report with specified parameters",
                ["arguments"] = new JArray
                {
                    new JObject
                    {
                        ["name"] = "report_type",
                        ["description"] = "Type of report to generate (daily, weekly, monthly)",
                        ["required"] = true
                    },
                    new JObject
                    {
                        ["name"] = "date_range",
                        ["description"] = "Date range for the report",
                        ["required"] = true
                    },
                    new JObject
                    {
                        ["name"] = "include_charts",
                        ["description"] = "Whether to include visualizations",
                        ["required"] = false
                    }
                }
            }

            // Add more prompts here following the same pattern
        };
    }

    #endregion

    /// <summary>
    /// Main entry point for the custom connector
    /// Routes requests to either AI Agent mode or traditional MCP mode
    /// </summary>
    public override async Task<HttpResponseMessage> ExecuteAsync()
    {
        // Log the incoming operation
        this.Context.Logger?.LogInformation($"Connector: Processing operation {this.Context.OperationId}");

        // Handle schema request for dynamic schema
        if (this.Context.OperationId == "GetSchema")
        {
            return HandleGetSchema();
        }

        // Check if this is the InvokeMCP operation (supports both Agent and MCP modes)
        if (this.Context.OperationId == "InvokeMCP")
        {
            return await RouteRequest().ConfigureAwait(false);
        }

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
    /// Returns dynamic schema for Power Automate UI with enumerated choices
    /// </summary>
    private HttpResponseMessage HandleGetSchema()
    {
        this.Context.Logger?.LogInformation("Returning dynamic schema");

        var schema = new JObject
        {
            ["schema"] = new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["mode"] = new JObject
                    {
                        ["type"] = "string",
                        ["title"] = "Execution Mode",
                        ["description"] = "Select how the agent should process your request",
                        ["enum"] = new JArray { "orchestrate", "generate", "execute", "chat", "mcp" },
                        ["x-ms-summary"] = "Mode",
                        ["default"] = "orchestrate"
                    },
                    ["input"] = new JObject
                    {
                        ["type"] = "string",
                        ["title"] = "Input",
                        ["description"] = "Natural language request or structured input",
                        ["x-ms-summary"] = "Input"
                    },
                    ["context"] = new JObject
                    {
                        ["type"] = "object",
                        ["title"] = "Context (Optional)",
                        ["description"] = "Additional context like conversation history, system prompts, or resources",
                        ["x-ms-summary"] = "Context",
                        ["x-ms-visibility"] = "advanced"
                    },
                    ["options"] = new JObject
                    {
                        ["type"] = "object",
                        ["title"] = "Options (Optional)",
                        ["description"] = "Execution options like temperature, max tokens, model selection",
                        ["x-ms-summary"] = "Options",
                        ["x-ms-visibility"] = "advanced",
                        ["properties"] = new JObject
                        {
                            ["model"] = new JObject
                            {
                                ["type"] = "string",
                                ["description"] = "Override AI model",
                                ["enum"] = new JArray { "", "gpt-4-turbo", "gpt-4", "gpt-3.5-turbo", "gpt-4o", "gpt-4o-mini" }
                            },
                            ["temperature"] = new JObject
                            {
                                ["type"] = "number",
                                ["description"] = "AI creativity (0.0-2.0)",
                                ["default"] = 0.7
                            },
                            ["maxTokens"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "Max response length",
                                ["default"] = 1000
                            },
                            ["autoExecuteTools"] = new JObject
                            {
                                ["type"] = "boolean",
                                ["description"] = "Auto-execute tools",
                                ["default"] = true
                            }
                        }
                    }
                },
                ["required"] = new JArray { "input" }
            }
        };

        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Content = CreateJsonContent(schema.ToString());
        return response;
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
                return CreateAgentErrorResponse("Invalid JSON in request body", null);
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

                case "resources/templates/list":
                    return HandleResourceTemplatesList(requestId);

                case "resources/read":
                    return await HandleResourcesRead(requestJson, requestId).ConfigureAwait(false);

                case "resources/subscribe":
                    return HandleResourcesSubscribe(requestJson, requestId);

                case "resources/unsubscribe":
                    return HandleResourcesUnsubscribe(requestJson, requestId);

                // Prompt methods
                case "prompts/list":
                    return HandlePromptsList(requestId);

                case "prompts/get":
                    return HandlePromptsGet(requestJson, requestId);

                // Completion method (for resource templates and prompt arguments)
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

    #region AI Agent Handler

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
            var mode = requestJson["mode"]?.ToString() ?? MODE_ORCHESTRATE;
            var input = requestJson["input"]?.ToString();
            var context = requestJson["context"] as JObject ?? new JObject();
            var options = requestJson["options"] as JObject ?? new JObject();

            if (string.IsNullOrEmpty(input))
            {
                return CreateAgentErrorResponse("'input' field is required", null);
            }

            this.Context.Logger?.LogInformation($"Agent Mode: {mode}, Input: {input}");

            // Extract options with defaults
            var autoExecuteTools = options["autoExecuteTools"]?.Value<bool>() ?? true;
            var maxToolCalls = options["maxToolCalls"]?.Value<int>() ?? DEFAULT_MAX_TOOL_CALLS;
            var includeToolResults = options["includeToolResults"]?.Value<bool>() ?? true;
            var temperature = options["temperature"]?.Value<double>() ?? DEFAULT_TEMPERATURE;
            var maxTokens = options["maxTokens"]?.Value<int>() ?? DEFAULT_MAX_TOKENS;

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
            
            var model = options["model"]?.ToString();
            if (string.IsNullOrEmpty(model) || model == "")
            {
                model = context["model"]?.ToString() ?? DEFAULT_MODEL;
            }

            if (string.IsNullOrEmpty(apiKey) || apiKey == "YOUR_OPENAI_API_KEY_HERE")
            {
                return CreateAgentErrorResponse(
                    "API key not configured. Please set your OpenAI API key in the script.csx file (around line 595) or provide it in context.apiKey.",
                    "Look for the CONFIGURATION section in HandleAgentRequest method - set apiKey variable"
                );
            }

            // Route to appropriate mode handler
            JObject agentResponse;
            switch (mode.ToLowerInvariant())
            {
                case MODE_ORCHESTRATE:
                    agentResponse = await HandleOrchestrate(input, context, apiKey, model, baseUrl, temperature, maxTokens, autoExecuteTools, maxToolCalls, includeToolResults).ConfigureAwait(false);
                    break;

                case MODE_GENERATE:
                    agentResponse = await HandleGenerate(input, context, apiKey, model, baseUrl, temperature, maxTokens).ConfigureAwait(false);
                    break;

                case MODE_EXECUTE:
                    agentResponse = await HandleExecute(input, context, apiKey, model, baseUrl, temperature, maxTokens).ConfigureAwait(false);
                    break;

                case MODE_CHAT:
                    agentResponse = await HandleChat(input, context, apiKey, model, baseUrl, temperature, maxTokens, autoExecuteTools, maxToolCalls).ConfigureAwait(false);
                    break;

                default:
                    return CreateAgentErrorResponse($"Unknown mode: {mode}. Valid modes are: orchestrate, generate, execute, chat", null);
            }

            // Add metadata
            var duration = (DateTime.UtcNow - startTime).TotalSeconds;
            agentResponse["metadata"] = new JObject
            {
                ["duration"] = $"{duration:F2}s",
                ["model"] = model,
                ["mode"] = mode
            };

            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = CreateJsonContent(agentResponse.ToString());
            return response;
        }
        catch (Exception ex)
        {
            this.Context.Logger?.LogError($"Agent request error: {ex.Message}\n{ex.StackTrace}");
            return CreateAgentErrorResponse($"Agent execution failed: {ex.Message}", ex.StackTrace);
        }
    }

    #endregion

    #region AI Orchestrator (OpenAI Integration)

    /// <summary>
    /// Handles orchestrate mode - AI plans and executes tools automatically
    /// </summary>
    private async Task<JObject> HandleOrchestrate(string input, JObject context, string apiKey, string model, string baseUrl, double temperature, int maxTokens, bool autoExecute, int maxToolCalls, bool includeResults)
    {
        this.Context.Logger?.LogInformation("Orchestrate mode: Starting");

        var toolCalls = new JArray();
        var plan = "";
        var response = "";
        
        // Get available tools
        var availableTools = context["availableTools"] as JArray;
        var tools = GetDefinedTools();
        
        // Filter tools if specified
        if (availableTools != null && availableTools.Count > 0)
        {
            var allowedToolNames = availableTools.Select(t => t.ToString()).ToList();
            var filteredTools = new JArray();
            foreach (var tool in tools)
            {
                if (allowedToolNames.Contains(tool["name"]?.ToString()))
                {
                    filteredTools.Add(tool);
                }
            }
            tools = filteredTools;
        }

        // Convert MCP tools to OpenAI function format
        var openAIFunctions = ConvertMCPToolsToOpenAIFunctions(tools);
        
        // Build messages for OpenAI
        var messages = BuildMessagesForOrchestration(input, context);
        
        // Call OpenAI with function calling
        var (aiResponse, executedTools, tokensUsed, cost) = await ExecuteOpenAIWithFunctions(
            apiKey, model, baseUrl, messages, openAIFunctions, temperature, maxTokens, autoExecute, maxToolCalls
        ).ConfigureAwait(false);
        
        response = aiResponse;
        toolCalls = executedTools;
        
        return new JObject
        {
            ["response"] = response,
            ["mode"] = MODE_ORCHESTRATE,
            ["execution"] = new JObject
            {
                ["plan"] = plan,
                ["toolCalls"] = includeResults ? toolCalls : new JArray(),
                ["toolsExecuted"] = toolCalls.Count
            },
            ["metadata"] = new JObject
            {
                ["tokensUsed"] = tokensUsed,
                ["estimatedCost"] = cost
            },
            ["error"] = null
        };
    }

    /// <summary>
    /// Handles generate mode - Pure generation with optional context
    /// </summary>
    private async Task<JObject> HandleGenerate(string input, JObject context, string apiKey, string model, string baseUrl, double temperature, int maxTokens)
    {
        this.Context.Logger?.LogInformation("Generate mode: Starting");

        // Get resources if specified
        var resourceUris = context["resources"] as JArray;
        var resourceContents = new JArray();
        
        if (resourceUris != null)
        {
            foreach (var uri in resourceUris)
            {
                try
                {
                    var (content, mimeType) = await ReadResourceByUri(uri.ToString()).ConfigureAwait(false);
                    resourceContents.Add(new JObject
                    {
                        ["uri"] = uri,
                        ["content"] = content.ToString()
                    });
                }
                catch (Exception ex)
                {
                    this.Context.Logger?.LogWarning($"Failed to load resource {uri}: {ex.Message}");
                }
            }
        }
        
        // Build messages with resources as context
        var messages = new JArray
        {
            new JObject
            {
                ["role"] = "system",
                ["content"] = context["systemPrompt"]?.ToString() ?? "You are a helpful assistant that generates responses based on provided context."
            }
        };
        
        // Add conversation history if provided
        var history = context["conversationHistory"] as JArray;
        if (history != null)
        {
            foreach (var msg in history)
            {
                messages.Add(msg);
            }
        }
        
        // Add resources as context
        if (resourceContents.Count > 0)
        {
            var contextMsg = "Context information:\n";
            foreach (var res in resourceContents)
            {
                contextMsg += $"\nFrom {res["uri"]}:\n{res["content"]}\n";
            }
            messages.Add(new JObject
            {
                ["role"] = "system",
                ["content"] = contextMsg
            });
        }
        
        // Add user input
        messages.Add(new JObject
        {
            ["role"] = "user",
            ["content"] = input
        });
        
        // Call OpenAI without functions
        var (response, tokensUsed, cost) = await ExecuteOpenAICompletion(
            apiKey, model, baseUrl, messages, temperature, maxTokens
        ).ConfigureAwait(false);
        
        return new JObject
        {
            ["response"] = response,
            ["mode"] = MODE_GENERATE,
            ["execution"] = new JObject
            {
                ["resourcesAccessed"] = resourceUris ?? new JArray(),
                ["resourceCount"] = resourceContents.Count
            },
            ["metadata"] = new JObject
            {
                ["tokensUsed"] = tokensUsed,
                ["estimatedCost"] = cost
            },
            ["error"] = null
        };
    }

    /// <summary>
    /// Handles execute mode - Direct tool execution with AI parameter mapping
    /// </summary>
    private async Task<JObject> HandleExecute(string input, JObject context, string apiKey, string model, string baseUrl, double temperature, int maxTokens)
    {
        this.Context.Logger?.LogInformation("Execute mode: Starting");

        var tools = GetDefinedTools();
        var openAIFunctions = ConvertMCPToolsToOpenAIFunctions(tools);
        
        var messages = new JArray
        {
            new JObject
            {
                ["role"] = "system",
                ["content"] = "You are a tool executor. Based on the user's request, call the appropriate tool with correct parameters. Call only ONE tool unless explicitly asked for multiple."
            },
            new JObject
            {
                ["role"] = "user",
                ["content"] = input
            }
        };
        
        // Force function call
        var (aiResponse, executedTools, tokensUsed, cost) = await ExecuteOpenAIWithFunctions(
            apiKey, model, baseUrl, messages, openAIFunctions, temperature, maxTokens, true, 1
        ).ConfigureAwait(false);
        
        return new JObject
        {
            ["response"] = aiResponse,
            ["mode"] = MODE_EXECUTE,
            ["execution"] = new JObject
            {
                ["toolCalls"] = executedTools
            },
            ["metadata"] = new JObject
            {
                ["tokensUsed"] = tokensUsed,
                ["estimatedCost"] = cost
            },
            ["error"] = null
        };
    }

    /// <summary>
    /// Handles chat mode - Conversational with memory and tool access
    /// </summary>
    private async Task<JObject> HandleChat(string input, JObject context, string apiKey, string model, string baseUrl, double temperature, int maxTokens, bool autoExecute, int maxToolCalls)
    {
        this.Context.Logger?.LogInformation("Chat mode: Starting");

        var tools = GetDefinedTools();
        var openAIFunctions = ConvertMCPToolsToOpenAIFunctions(tools);
        
        // Build messages with conversation history
        var messages = new JArray
        {
            new JObject
            {
                ["role"] = "system",
                ["content"] = context["systemPrompt"]?.ToString() ?? "You are a helpful assistant. Use available tools when needed to answer questions accurately."
            }
        };
        
        // Add conversation history
        var history = context["conversationHistory"] as JArray;
        if (history != null)
        {
            foreach (var msg in history)
            {
                messages.Add(msg);
            }
        }
        
        // Add current user message
        messages.Add(new JObject
        {
            ["role"] = "user",
            ["content"] = input
        });
        
        // Call OpenAI with functions
        var (aiResponse, executedTools, tokensUsed, cost) = await ExecuteOpenAIWithFunctions(
            apiKey, model, baseUrl, messages, openAIFunctions, temperature, maxTokens, autoExecute, maxToolCalls
        ).ConfigureAwait(false);
        
        // Build updated conversation history
        messages.Add(new JObject
        {
            ["role"] = "assistant",
            ["content"] = aiResponse
        });
        
        return new JObject
        {
            ["response"] = aiResponse,
            ["mode"] = MODE_CHAT,
            ["execution"] = new JObject
            {
                ["toolCalls"] = executedTools,
                ["toolsExecuted"] = executedTools.Count
            },
            ["conversationHistory"] = messages,
            ["metadata"] = new JObject
            {
                ["tokensUsed"] = tokensUsed,
                ["estimatedCost"] = cost
            },
            ["error"] = null
        };
    }

    #endregion

    #region OpenAI API Integration

    /// <summary>
    /// Executes OpenAI API call with function calling support
    /// </summary>
    private async Task<(string response, JArray toolCalls, int tokensUsed, string cost)> ExecuteOpenAIWithFunctions(
        string apiKey, string model, string baseUrl, JArray messages, JArray functions, 
        double temperature, int maxTokens, bool autoExecute, int maxIterations)
    {
        var allToolCalls = new JArray();
        var totalTokens = 0;
        var iterations = 0;
        
        while (iterations < maxIterations)
        {
            iterations++;
            this.Context.Logger?.LogInformation($"OpenAI call iteration {iterations}");
            
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
            
            // Call OpenAI API
            var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
            request.Headers.Add("Authorization", $"Bearer {apiKey}");
            request.Content = new StringContent(requestBody.ToString(), System.Text.Encoding.UTF8, "application/json");
            
            var response = await this.Context.SendAsync(request, this.CancellationToken).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            
            if (!response.IsSuccessStatusCode)
            {
                this.Context.Logger?.LogError($"OpenAI API error: {responseBody}");
                throw new Exception($"OpenAI API error: {response.StatusCode} - {responseBody}");
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
                var cost = EstimateCost(model, totalTokens);
                return (content, allToolCalls, totalTokens, cost);
            }
            
            // Execute tool calls if auto-execute is enabled
            if (!autoExecute)
            {
                var cost = EstimateCost(model, totalTokens);
                return ("Tool calls ready but auto-execute is disabled", allToolCalls, totalTokens, cost);
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
        var finalCost = EstimateCost(model, totalTokens);
        return ("Max tool call iterations reached", allToolCalls, totalTokens, finalCost);
    }

    /// <summary>
    /// Executes OpenAI API call without function calling (pure generation)
    /// </summary>
    private async Task<(string response, int tokensUsed, string cost)> ExecuteOpenAICompletion(
        string apiKey, string model, string baseUrl, JArray messages, double temperature, int maxTokens)
    {
        var requestBody = new JObject
        {
            ["model"] = model,
            ["messages"] = messages,
            ["temperature"] = temperature,
            ["max_tokens"] = maxTokens
        };
        
        var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
        request.Headers.Add("Authorization", $"Bearer {apiKey}");
        request.Content = new StringContent(requestBody.ToString(), System.Text.Encoding.UTF8, "application/json");
        
        var response = await this.Context.SendAsync(request, this.CancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        
        if (!response.IsSuccessStatusCode)
        {
            this.Context.Logger?.LogError($"OpenAI API error: {responseBody}");
            throw new Exception($"OpenAI API error: {response.StatusCode} - {responseBody}");
        }
        
        var responseJson = JObject.Parse(responseBody);
        var usage = responseJson["usage"];
        var totalTokens = usage?["total_tokens"]?.Value<int>() ?? 0;
        var content = responseJson["choices"]?[0]?["message"]?["content"]?.ToString() ?? "";
        var cost = EstimateCost(model, totalTokens);
        
        return (content, totalTokens, cost);
    }

    /// <summary>
    /// Converts MCP tool definitions to OpenAI function calling format
    /// </summary>
    private JArray ConvertMCPToolsToOpenAIFunctions(JArray mcpTools)
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
    /// Builds OpenAI messages for orchestration mode
    /// </summary>
    private JArray BuildMessagesForOrchestration(string input, JObject context)
    {
        var messages = new JArray
        {
            new JObject
            {
                ["role"] = "system",
                ["content"] = context["systemPrompt"]?.ToString() ?? 
                    "You are an AI orchestrator. Analyze the user's request and use available tools to accomplish the task. " +
                    "Call tools in the right order and synthesize results into a helpful response."
            }
        };
        
        var history = context["conversationHistory"] as JArray;
        if (history != null)
        {
            foreach (var msg in history)
            {
                messages.Add(msg);
            }
        }
        
        messages.Add(new JObject
        {
            ["role"] = "user",
            ["content"] = input
        });
        
        return messages;
    }

    /// <summary>
    /// Executes a tool by name (routes to appropriate tool handler)
    /// </summary>
    private async Task<JObject> ExecuteToolByName(string toolName, JObject arguments)
    {
        switch (toolName)
        {
            case "get_weather":
                return await ExecuteGetWeatherTool(arguments).ConfigureAwait(false);
            
            case "search_data":
                return await ExecuteSearchDataTool(arguments).ConfigureAwait(false);
            
            // Add more tool handlers here
            
            default:
                throw new Exception($"Unknown tool: {toolName}");
        }
    }

    /// <summary>
    /// Reads a resource by URI (routes based on URI scheme)
    /// </summary>
    private async Task<(JObject content, string mimeType)> ReadResourceByUri(string uri)
    {
        if (uri.StartsWith("config://"))
            return await ReadConfigResource(uri).ConfigureAwait(false);
        else if (uri.StartsWith("db://"))
            return await ReadDatabaseResource(uri).ConfigureAwait(false);
        else if (uri.StartsWith("user://"))
            return await ReadUserResource(uri).ConfigureAwait(false);
        else if (uri.StartsWith("docs://"))
            return await ReadDocumentResource(uri).ConfigureAwait(false);
        else
            throw new Exception($"Unsupported resource URI scheme: {uri}");
    }

    /// <summary>
    /// Estimates cost based on model and tokens (approximate)
    /// </summary>
    private string EstimateCost(string model, int tokens)
    {
        // Approximate pricing (as of 2024)
        double costPer1kTokens = 0.0;
        
        if (model.Contains("gpt-4"))
            costPer1kTokens = 0.03; // GPT-4 average
        else if (model.Contains("gpt-3.5"))
            costPer1kTokens = 0.002; // GPT-3.5
        else
            costPer1kTokens = 0.01; // Default estimate
        
        var cost = (tokens / 1000.0) * costPer1kTokens;
        return $"${cost:F4}";
    }

    /// <summary>
    /// Creates an agent error response
    /// </summary>
    private HttpResponseMessage CreateAgentErrorResponse(string message, string details)
    {
        this.Context.Logger?.LogError($"Agent error: {message}");
        
        var errorResponse = new JObject
        {
            ["response"] = null,
            ["error"] = message,
            ["details"] = details,
            ["mode"] = null
        };
        
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Content = CreateJsonContent(errorResponse.ToString());
        return response;
    }

    #endregion

    #region MCP Method Handlers

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

        // Check cache first
        var cachedResponse = GetCachedToolList();
        if (cachedResponse != null)
        {
            this.Context.Logger?.LogInformation("Returning cached tools/list response");
            var cachedJson = JObject.Parse(cachedResponse);
            cachedJson["id"] = requestId;
            
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = CreateJsonContent(cachedJson.ToString());
            return response;
        }

        // Build tool list from defined tools
        var result = new JObject
        {
            ["tools"] = GetDefinedTools()
        };

        var responseObj = new JObject
        {
            ["jsonrpc"] = "2.0",
            ["result"] = result,
            ["id"] = requestId
        };

        // Cache the response
        CacheToolList(responseObj.ToString());
        this.Context.Logger?.LogInformation("Cached tools/list response");

        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK);
        httpResponse.Content = CreateJsonContent(responseObj.ToString());
        return httpResponse;
    }

    /// <summary>
    /// Handles the tools/call request and executes the specified tool
    /// </summary>
    private async Task<HttpResponseMessage> HandleToolsCall(JObject requestJson, JToken requestId)
    {
        this.Context.Logger?.LogInformation("Handling tools/call request");

        var paramsObj = requestJson["params"] as JObject;
        if (paramsObj == null)
        {
            return CreateErrorResponse(-32602, "Invalid params: 'params' object required", requestId);
        }

        var toolName = paramsObj["name"]?.ToString();
        var arguments = paramsObj["arguments"] as JObject;

        if (string.IsNullOrEmpty(toolName))
        {
            return CreateErrorResponse(-32602, "Invalid params: 'name' field required", requestId);
        }

        this.Context.Logger?.LogInformation($"Calling tool: {toolName}");

        // Route to appropriate tool handler
        JObject toolResult;
        try
        {
            switch (toolName)
            {
                case "get_weather":
                    toolResult = await ExecuteGetWeatherTool(arguments).ConfigureAwait(false);
                    break;

                case "search_data":
                    toolResult = await ExecuteSearchDataTool(arguments).ConfigureAwait(false);
                    break;

                // Add more tool handlers here

                default:
                    return CreateErrorResponse(-32602, $"Unknown tool: {toolName}", requestId);
            }

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
        catch (Exception ex)
        {
            this.Context.Logger?.LogError($"Tool execution error: {ex.Message}");
            return CreateErrorResponse(-32000, $"Tool execution failed: {ex.Message}", requestId);
        }
    }

    #endregion

    #region Resource Method Handlers

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
    /// Handles the resources/templates/list request and returns defined resource templates
    /// </summary>
    private HttpResponseMessage HandleResourceTemplatesList(JToken requestId)
    {
        this.Context.Logger?.LogInformation("Handling resources/templates/list request");

        try
        {
            var result = new JObject
            {
                ["resourceTemplates"] = GetDefinedResourceTemplates()
            };

            return CreateSuccessResponse(result, requestId);
        }
        catch (Exception ex)
        {
            this.Context.Logger?.LogError($"Error listing resource templates: {ex.Message}");
            return CreateErrorResponse(-32603, $"Failed to list resource templates: {ex.Message}", requestId);
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

            // Route to appropriate resource handler based on URI scheme
            JObject resourceContent;
            string mimeType;

            if (uri.StartsWith("config://"))
            {
                (resourceContent, mimeType) = await ReadConfigResource(uri).ConfigureAwait(false);
            }
            else if (uri.StartsWith("db://"))
            {
                (resourceContent, mimeType) = await ReadDatabaseResource(uri).ConfigureAwait(false);
            }
            else if (uri.StartsWith("user://"))
            {
                (resourceContent, mimeType) = await ReadUserResource(uri).ConfigureAwait(false);
            }
            else if (uri.StartsWith("docs://"))
            {
                (resourceContent, mimeType) = await ReadDocumentResource(uri).ConfigureAwait(false);
            }
            else
            {
                return CreateErrorResponse(-32602, $"Unsupported resource URI scheme: {uri}", requestId);
            }

            // Return resource contents in MCP format
            var result = new JObject
            {
                ["contents"] = new JArray
                {
                    new JObject
                    {
                        ["uri"] = uri,
                        ["mimeType"] = mimeType,
                        ["text"] = resourceContent.ToString(Formatting.None)
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
    /// Handles the resources/subscribe request
    /// </summary>
    private HttpResponseMessage HandleResourcesSubscribe(JObject requestJson, JToken requestId)
    {
        this.Context.Logger?.LogInformation("Handling resources/subscribe request");

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

            this.Context.Logger?.LogInformation($"Subscribing to resource: {uri}");

            // TODO: Implement subscription logic
            // 1. Validate the URI exists
            // 2. Store subscription information
            // 3. Set up monitoring for changes
            // 4. Return success response
            //
            // Note: Power Platform custom connectors don't natively support SSE,
            // so subscriptions may need to use polling or webhooks instead

            return CreateSuccessResponse(new JObject(), requestId);
        }
        catch (Exception ex)
        {
            this.Context.Logger?.LogError($"Resource subscribe error: {ex.Message}");
            return CreateErrorResponse(-32603, $"Failed to subscribe: {ex.Message}", requestId);
        }
    }

    /// <summary>
    /// Handles the resources/unsubscribe request
    /// </summary>
    private HttpResponseMessage HandleResourcesUnsubscribe(JObject requestJson, JToken requestId)
    {
        this.Context.Logger?.LogInformation("Handling resources/unsubscribe request");

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

            this.Context.Logger?.LogInformation($"Unsubscribing from resource: {uri}");

            // TODO: Implement unsubscription logic
            // 1. Validate the subscription exists
            // 2. Remove subscription information
            // 3. Clean up monitoring
            // 4. Return success response

            return CreateSuccessResponse(new JObject(), requestId);
        }
        catch (Exception ex)
        {
            this.Context.Logger?.LogError($"Resource unsubscribe error: {ex.Message}");
            return CreateErrorResponse(-32603, $"Failed to unsubscribe: {ex.Message}", requestId);
        }
    }

    #endregion

    #region Prompt Method Handlers

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

            // Route to appropriate prompt handler
            JArray messages;
            string description;

            switch (promptName)
            {
                case "analyze_data":
                    (messages, description) = BuildAnalyzeDataPrompt(arguments);
                    break;

                case "generate_report":
                    (messages, description) = BuildGenerateReportPrompt(arguments);
                    break;

                default:
                    return CreateErrorResponse(-32602, $"Unknown prompt: {promptName}", requestId);
            }

            // Return prompt with messages
            var result = new JObject
            {
                ["description"] = description,
                ["messages"] = messages
            };

            return CreateSuccessResponse(result, requestId);
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

    #endregion

    #region Completion Handler

    /// <summary>
    /// Handles the completion/complete request for parameter completion
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

            // Get completion values based on type
            JArray completions;

            if (refType == "ref/resource")
            {
                var uri = refObj["uri"]?.ToString();
                completions = await GetResourceParameterCompletions(uri, argumentName, argumentValue).ConfigureAwait(false);
            }
            else if (refType == "ref/prompt")
            {
                var promptName = refObj["name"]?.ToString();
                completions = GetPromptArgumentCompletions(promptName, argumentName, argumentValue);
            }
            else
            {
                return CreateErrorResponse(-32602, $"Unsupported ref type: {refType}", requestId);
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

    #endregion

    #region Tool Execution Methods

    /// <summary>
    /// Executes the get_weather tool
    /// Maps to an external weather API
    /// </summary>
    private async Task<JObject> ExecuteGetWeatherTool(JObject arguments)
    {
        // TODO: Implement external API call logic
        // 1. Extract parameters from arguments
        //    var location = arguments["location"]?.ToString();
        //    var units = arguments["units"]?.ToString() ?? "metric";
        //
        // 2. Validate required parameters
        //    if (string.IsNullOrEmpty(location))
        //        throw new ArgumentException("location is required");
        //
        // 3. Build external API request
        //    var apiUrl = $"https://api.weather.com/v1/current?location={location}&units={units}";
        //    var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
        //    request.Headers.Add("X-API-Key", "your-api-key");
        //
        // 4. Call external API
        //    var response = await this.Context.SendAsync(request, this.CancellationToken).ConfigureAwait(false);
        //
        // 5. Parse and transform response
        //    var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        //    var weatherData = JObject.Parse(responseBody);
        //
        // 6. Return formatted result
        //    return new JObject
        //    {
        //        ["location"] = location,
        //        ["temperature"] = weatherData["temp"],
        //        ["conditions"] = weatherData["description"],
        //        ["units"] = units
        //    };

        // Placeholder implementation
        return new JObject
        {
            ["message"] = "Tool implementation placeholder - replace with actual API call",
            ["tool"] = "get_weather",
            ["arguments"] = arguments
        };
    }

    /// <summary>
    /// Executes the search_data tool
    /// Maps to an external search API
    /// </summary>
    private async Task<JObject> ExecuteSearchDataTool(JObject arguments)
    {
        // TODO: Implement external API call logic
        // 1. Extract parameters from arguments
        //    var query = arguments["query"]?.ToString();
        //    var limit = arguments["limit"]?.Value<int>() ?? 10;
        //
        // 2. Validate required parameters
        //    if (string.IsNullOrEmpty(query))
        //        throw new ArgumentException("query is required");
        //
        // 3. Build external API request
        //    var apiUrl = $"https://api.example.com/search?q={Uri.EscapeDataString(query)}&limit={limit}";
        //    var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
        //    request.Headers.Add("Authorization", "Bearer your-token");
        //
        // 4. Call external API
        //    var response = await this.Context.SendAsync(request, this.CancellationToken).ConfigureAwait(false);
        //
        // 5. Parse and transform response
        //    var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        //    var searchResults = JArray.Parse(responseBody);
        //
        // 6. Return formatted result
        //    return new JObject
        //    {
        //        ["query"] = query,
        //        ["results"] = searchResults,
        //        ["count"] = searchResults.Count
        //    };

        // Placeholder implementation
        return new JObject
        {
            ["message"] = "Tool implementation placeholder - replace with actual API call",
            ["tool"] = "search_data",
            ["arguments"] = arguments
        };
    }

    // Add more tool execution methods here following the same pattern

    #endregion

    #region Resource Reading Methods

    /// <summary>
    /// Reads a config:// resource
    /// </summary>
    private async Task<(JObject content, string mimeType)> ReadConfigResource(string uri)
    {
        // TODO: Implement config resource reading logic
        // 1. Parse the URI to extract the config path
        //    var configPath = uri.Substring("config://".Length);
        //
        // 2. Build external API request to fetch configuration
        //    var apiUrl = $"https://api.example.com/config/{configPath}";
        //    var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
        //
        // 3. Call external API
        //    var response = await this.Context.SendAsync(request, this.CancellationToken).ConfigureAwait(false);
        //
        // 4. Parse and return configuration data
        //    var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        //    var configData = JObject.Parse(responseBody);
        //    return (configData, "application/json");

        // Placeholder implementation
        return (new JObject
        {
            ["message"] = "Resource reading placeholder - replace with actual implementation",
            ["uri"] = uri
        }, "application/json");
    }

    /// <summary>
    /// Reads a db:// resource
    /// </summary>
    private async Task<(JObject content, string mimeType)> ReadDatabaseResource(string uri)
    {
        // TODO: Implement database resource reading logic
        // 1. Parse the URI to extract schema/table information
        //    var parts = uri.Substring("db://".Length).Split('/');
        //    var schema = parts[0];
        //    var table = parts.Length > 1 ? parts[1] : null;
        //
        // 2. Build external API request to fetch schema/data
        //    var apiUrl = $"https://api.example.com/database/{schema}/{table}";
        //    var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
        //
        // 3. Call external API
        //    var response = await this.Context.SendAsync(request, this.CancellationToken).ConfigureAwait(false);
        //
        // 4. Parse and return database information
        //    var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        //    var dbData = JObject.Parse(responseBody);
        //    return (dbData, "application/json");

        // Placeholder implementation
        return (new JObject
        {
            ["message"] = "Resource reading placeholder - replace with actual implementation",
            ["uri"] = uri
        }, "application/json");
    }

    /// <summary>
    /// Reads a user:// resource (resource template)
    /// </summary>
    private async Task<(JObject content, string mimeType)> ReadUserResource(string uri)
    {
        // TODO: Implement user resource reading logic
        // 1. Parse the URI to extract user ID
        //    var match = System.Text.RegularExpressions.Regex.Match(uri, @"user://profile/(.+)");
        //    if (!match.Success)
        //        throw new ArgumentException("Invalid user URI format");
        //    var userId = match.Groups[1].Value;
        //
        // 2. Build external API request to fetch user profile
        //    var apiUrl = $"https://api.example.com/users/{userId}";
        //    var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
        //
        // 3. Call external API
        //    var response = await this.Context.SendAsync(request, this.CancellationToken).ConfigureAwait(false);
        //
        // 4. Parse and return user data
        //    var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        //    var userData = JObject.Parse(responseBody);
        //    return (userData, "application/json");

        // Placeholder implementation
        return (new JObject
        {
            ["message"] = "Resource reading placeholder - replace with actual implementation",
            ["uri"] = uri
        }, "application/json");
    }

    /// <summary>
    /// Reads a docs:// resource (resource template)
    /// </summary>
    private async Task<(JObject content, string mimeType)> ReadDocumentResource(string uri)
    {
        // TODO: Implement document resource reading logic
        // 1. Parse the URI to extract category and document ID
        //    var match = System.Text.RegularExpressions.Regex.Match(uri, @"docs://category/(.+)/document/(.+)");
        //    if (!match.Success)
        //        throw new ArgumentException("Invalid document URI format");
        //    var category = match.Groups[1].Value;
        //    var docId = match.Groups[2].Value;
        //
        // 2. Build external API request to fetch document
        //    var apiUrl = $"https://api.example.com/documents/{category}/{docId}";
        //    var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
        //
        // 3. Call external API
        //    var response = await this.Context.SendAsync(request, this.CancellationToken).ConfigureAwait(false);
        //
        // 4. Parse and return document content
        //    var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        //    // If it's markdown, wrap in JSON
        //    var docContent = new JObject { ["content"] = responseBody };
        //    return (docContent, "text/markdown");

        // Placeholder implementation
        return (new JObject
        {
            ["message"] = "Resource reading placeholder - replace with actual implementation",
            ["uri"] = uri
        }, "text/markdown");
    }

    #endregion

    #region Prompt Building Methods

    /// <summary>
    /// Builds the analyze_data prompt with provided arguments
    /// </summary>
    private (JArray messages, string description) BuildAnalyzeDataPrompt(JObject arguments)
    {
        // TODO: Implement prompt building logic
        // 1. Extract and validate arguments
        //    var source = arguments["source"]?.ToString();
        //    if (string.IsNullOrEmpty(source))
        //        throw new ArgumentException("source argument is required");
        //    var filters = arguments["filters"]?.ToString();
        //    var outputFormat = arguments["output_format"]?.ToString() ?? "summary";
        //
        // 2. Build prompt messages
        //    var messages = new JArray
        //    {
        //        new JObject
        //        {
        //            ["role"] = "user",
        //            ["content"] = new JObject
        //            {
        //                ["type"] = "text",
        //                ["text"] = $"Please analyze the data from {source}.\n" +
        //                          (string.IsNullOrEmpty(filters) ? "" : $"Apply these filters: {filters}\n") +
        //                          $"Provide a {outputFormat} analysis."
        //            }
        //        }
        //    };
        //
        // 3. Return messages and description
        //    return (messages, $"Analyzing data from {source}");

        // Placeholder implementation
        var messages = new JArray
        {
            new JObject
            {
                ["role"] = "user",
                ["content"] = new JObject
                {
                    ["type"] = "text",
                    ["text"] = "Prompt building placeholder - replace with actual implementation"
                }
            }
        };

        return (messages, "Data analysis prompt");
    }

    /// <summary>
    /// Builds the generate_report prompt with provided arguments
    /// </summary>
    private (JArray messages, string description) BuildGenerateReportPrompt(JObject arguments)
    {
        // TODO: Implement prompt building logic
        // 1. Extract and validate arguments
        //    var reportType = arguments["report_type"]?.ToString();
        //    if (string.IsNullOrEmpty(reportType))
        //        throw new ArgumentException("report_type argument is required");
        //    var dateRange = arguments["date_range"]?.ToString();
        //    if (string.IsNullOrEmpty(dateRange))
        //        throw new ArgumentException("date_range argument is required");
        //    var includeCharts = arguments["include_charts"]?.Value<bool>() ?? false;
        //
        // 2. Build prompt messages
        //    var messages = new JArray
        //    {
        //        new JObject
        //        {
        //            ["role"] = "user",
        //            ["content"] = new JObject
        //            {
        //                ["type"] = "text",
        //                ["text"] = $"Generate a {reportType} report for {dateRange}.\n" +
        //                          (includeCharts ? "Include charts and visualizations.\n" : "") +
        //                          "Provide comprehensive analysis with key insights."
        //            }
        //        }
        //    };
        //
        // 3. Return messages and description
        //    return (messages, $"Generating {reportType} report for {dateRange}");

        // Placeholder implementation
        var messages = new JArray
        {
            new JObject
            {
                ["role"] = "user",
                ["content"] = new JObject
                {
                    ["type"] = "text",
                    ["text"] = "Prompt building placeholder - replace with actual implementation"
                }
            }
        };

        return (messages, "Report generation prompt");
    }

    #endregion

    #region Completion Methods

    /// <summary>
    /// Gets parameter completion values for resource templates
    /// </summary>
    private async Task<JArray> GetResourceParameterCompletions(string uri, string paramName, string partialValue)
    {
        // TODO: Implement resource parameter completion logic
        // 1. Parse the resource template URI to understand the parameter
        //    if (uri.Contains("{userId}") && paramName == "userId")
        //    {
        //        // 2. Query external API for matching user IDs
        //        var apiUrl = $"https://api.example.com/users/search?q={Uri.EscapeDataString(partialValue)}&limit=10";
        //        var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
        //        var response = await this.Context.SendAsync(request, this.CancellationToken).ConfigureAwait(false);
        //        
        //        // 3. Parse results and return completion values
        //        var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        //        var users = JArray.Parse(responseBody);
        //        
        //        var completions = new JArray();
        //        foreach (var user in users)
        //        {
        //            completions.Add(new JObject
        //            {
        //                ["value"] = user["id"],
        //                ["label"] = user["name"]
        //            });
        //        }
        //        return completions;
        //    }

        // Placeholder implementation
        return new JArray
        {
            new JObject { ["value"] = "completion1", ["label"] = "Example Completion 1" },
            new JObject { ["value"] = "completion2", ["label"] = "Example Completion 2" }
        };
    }

    /// <summary>
    /// Gets argument completion values for prompts
    /// </summary>
    private JArray GetPromptArgumentCompletions(string promptName, string argName, string partialValue)
    {
        // TODO: Implement prompt argument completion logic
        // 1. Determine which prompt and argument needs completion
        //    if (promptName == "generate_report" && argName == "report_type")
        //    {
        //        // 2. Return predefined valid values
        //        var reportTypes = new[] { "daily", "weekly", "monthly", "quarterly", "annual" };
        //        var filtered = reportTypes.Where(rt => rt.StartsWith(partialValue, StringComparison.OrdinalIgnoreCase));
        //        
        //        var completions = new JArray();
        //        foreach (var type in filtered)
        //        {
        //            completions.Add(new JObject
        //            {
        //                ["value"] = type,
        //                ["label"] = char.ToUpper(type[0]) + type.Substring(1) + " Report"
        //            });
        //        }
        //        return completions;
        //    }

        // Placeholder implementation
        return new JArray
        {
            new JObject { ["value"] = "option1", ["label"] = "Option 1" },
            new JObject { ["value"] = "option2", ["label"] = "Option 2" }
        };
    }

    #endregion

    #region JSON-RPC Helper Methods

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

    #endregion

    #region JSON-RPC Validation
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

    #endregion

    #region JSON-RPC Validation
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

        return null; // Valid
    }

    #endregion

    #region Cache Management

    /// <summary>
    /// Gets cached tool list if available and not expired
    /// </summary>
    private string GetCachedToolList()
    {
        string cacheKey = GetCacheKey();
        
        lock (_cacheLock)
        {
            if (_toolCache.TryGetValue(cacheKey, out var cachedEntry))
            {
                if (!cachedEntry.IsExpired)
                {
                    return cachedEntry.ToolListJson;
                }
                else
                {
                    // Remove expired entry
                    _toolCache.Remove(cacheKey);
                    this.Context.Logger?.LogInformation("Removed expired tools/list cache entry");
                }
            }
        }
        
        return null;
    }

    /// <summary>
    /// Caches the tool list response
    /// </summary>
    private void CacheToolList(string toolListJson)
    {
        string cacheKey = GetCacheKey();
        
        lock (_cacheLock)
        {
            _toolCache[cacheKey] = new CachedToolList
            {
                ToolListJson = toolListJson,
                CachedAt = DateTime.UtcNow
            };
            
            // Cleanup: Remove expired entries (max 100 entries)
            if (_toolCache.Count > 100)
            {
                var expiredKeys = _toolCache
                    .Where(kvp => kvp.Value.IsExpired)
                    .Select(kvp => kvp.Key)
                    .ToList();
                    
                foreach (var key in expiredKeys)
                {
                    _toolCache.Remove(key);
                }
            }
        }
    }

    /// <summary>
    /// Generates a cache key based on the MCP server endpoint
    /// </summary>
    private string GetCacheKey()
    {
        // Use the request URI host as the cache key
        // This allows caching per MCP server endpoint
        return this.Context.Request.RequestUri.Host;
    }

    #endregion
}
