namespace Rhythm.Umbraco7.DataProcessor
{

    // Namespaces.
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Web.UI;
    using System.Web.UI.WebControls;
    using Umbraco.Core;
    using System.Net.Http.Formatting;
    using System.Web;
    using Umbraco.Core;
    using Umbraco.Web.Mvc;
    using Umbraco.Web.WebApi;
    using System.Web.Http;
    using System.Net;


    #region class ProcessorKinds

    /// <summary>
    /// The kinds of data processor inputs.
    /// </summary>
    /// <remarks>
    /// Node - Renders a multi-node tree picker. A user must pick exactly one node.
    /// Nodes - Renders a multi-node tree picker. A user must pick at least one node.
    /// Text - Renders a textstring.
    /// </remarks>
    public class ProcessorKinds
    {
        public const string Node = "Node";
        public const string Nodes = "Nodes";
        public const string Text = "Text";
    }

    #endregion


    #region class UmbracoDataProcessor

    /// <summary>
    /// The class to be used as an attribute that indicates a class is a data processor.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class UmbracoDataProcessor : System.Attribute
    {

        /// <summary>
        /// The label used when displaying the data processor to the user.
        /// </summary>
        public string Label {get; set; }

    }

    #endregion


    #region class ProcessorInput

    /// <summary>
    /// Stores information about the type of input used by a data processor.
    /// </summary>
    public class ProcessorInput
    {

        /// <summary>
        /// The kind of input (e.g., "Text" or "Nodes").
        /// </summary>
        public string Kind { get; set; }


        /// <summary>
        /// The label shown above the input field.
        /// </summary>
        public string Label { get; set; }

    }

    #endregion


    #region class ProcessResult

    /// <summary>
    /// Stores the result of processed inputs.
    /// </summary>
    public class ProcessResult
    {
        public string Message { get; set; }
    }

    #endregion


    #region class RhythmDataProcessorController

    /// <summary>
    /// The API controller for the data processor.
    /// </summary>
    [PluginController("Rhythm")]
    public class RhythmDataProcessorController : UmbracoApiController
    {

        /// <summary>
        /// Gets the list of data processors.
        /// </summary>
        /// <returns>The list of data processors.</returns>
        [AcceptVerbs("GET")]
        public string[] GetProcessors()
        {
            return GetDataProcessors().Select(x => x.Item2.Label).ToArray();
        }


        /// <summary>
        /// Gets the list of inputs for the specified data processor.
        /// </summary>
        /// <param name="processor">The label for the data processor.</param>
        /// <returns>The processor inputs.</returns>
        [AcceptVerbs("GET")]
        public ProcessorInput[] GetProcessorInputs([FromUri] string processor)
        {
            var processorType = GetDataProcessors()
                .Where(x => x.Item2.Label.InvariantEquals(processor)).FirstOrDefault().Item1;
            return GetProcessorInputs(processorType);
        }


        /// <summary>
        /// Processes the inputs using the specified data processor.
        /// </summary>
        /// <param name="processor">The label for the data processor.</param>
        /// <param name="data">The data to pass to the data processor.</param>
        /// <returns>The HTML message returned by the data processor.</returns>
        [AcceptVerbs("GET")]
        public ProcessResult ProcessInputs([FromUri] string processor, [FromUri] string[] data)
        {

            // Variables.
            var valid = true;
            var message = null as string;
            var inputs = new List<object>();


            // Get data processor.
            var dataProcessor = GetDataProcessors().Where(x => x.Item2.Label.InvariantEquals(processor)).FirstOrDefault();
            if(dataProcessor == null)
            {
                message = "<h2>Invalid data processor.</h2>";
                valid = false;
            }
            else
            {

                // Convert string data into valid inputs.
                var inputKinds = GetProcessorInputs(dataProcessor.Item1);
                for (var i = 0; i < data.Length; i++)
                {
                    var kind = inputKinds[i].Kind;
                    var dataItem = null as object;
                    if (kind == ProcessorKinds.Node)
                    {

                        // Node: Get node ID.
                        var tempInt = 0;
                        if (int.TryParse(data[i], out tempInt))
                        {
                            dataItem = tempInt;
                        }
                        else
                        {
                            valid = false;
                        }

                    }
                    else if (kind == ProcessorKinds.Nodes)
                    {

                        // Nodes: Get node ID's.
                        var tempInt = 0;
                        var ids = (data[i] ?? string.Empty).Split(", ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                        var listIds = new List<int>();
                        foreach (var id in ids)
                        {
                            if (int.TryParse(id, out tempInt))
                            {
                                listIds.Add(tempInt);
                            }
                            else
                            {
                                valid = false;
                            }
                        }
                        if (listIds.Count == 0)
                        {
                            valid = false;
                        }
                        dataItem = listIds.ToArray();

                    }
                    else if (kind == ProcessorKinds.Text)
                    {

                        // Text: Get string.
                        dataItem = data[i];

                    }
                    inputs.Add(dataItem);
                }


                // Message for invalid input.
                if (!valid)
                {
                    message = @"<h2>Error. Invalid input.</h2>";
                }

            }


            // Were all of the inputs valid?
            if (valid)
            {

                // Pass to the data processor.
                message = dataProcessor.Item1.GetMethod("ProcessInputs")
                    .Invoke(null, new [] {inputs} ) as string;

            }
            else
            {
                message = message ?? @"<h2>Unknown error.</h2>";
            }


            // Return result message.
            return new ProcessResult()
            {
                Message = message
            };

        }


        /// <summary>
        /// Gets the inputs for the specified data processor.
        /// </summary>
        /// <param name="processorType">The type for the data processor.</param>
        /// <returns>The inputs.</returns>
        private ProcessorInput[] GetProcessorInputs(Type processorType)
        {
            return processorType.GetMethod("Inputs").Invoke(null, null) as ProcessorInput[];
        }


        /// <summary>
        /// Gets the data processors in the current app domain.
        /// </summary>
        /// <returns>The type and attribute for each data processor.</returns>
        private List<Tuple<Type, UmbracoDataProcessor>> GetDataProcessors()
        {

            // Variables.
            var processors = new List<Tuple<Type, UmbracoDataProcessor>>();


            // Find all types in all assemblies that are marked as data processors.
            foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {

                // Variables.
                var types = new List<Type>() as IEnumerable<Type>;


                // Dynamic assemblies may throw an exception.
                try
                {
                    types = assembly.ExportedTypes;
                }
                catch { }


                // Look at the attributes on each type.
                foreach(var type in types)
                {
                    var attributes = type.GetCustomAttributes(typeof(UmbracoDataProcessor), false)
                        .Cast<UmbracoDataProcessor>();
                    if(attributes.Any())
                    {
                        var attribute = attributes.First();
                        processors.Add(new Tuple<Type,UmbracoDataProcessor>(type, attribute));
                    }
                }

            }


            // Return data processors.
            return processors;

        }

    }

    #endregion


    #region class BulkMoveNodes

    /// <summary>
    /// A data processor that moves multiple nodes to under a new parent node.
    /// </summary>
    [UmbracoDataProcessor(Label = "Bulk Move Nodes")]
    public static class BulkMoveNodes
    {

        /// <summary>
        /// The types of inputs required by this data processor.
        /// </summary>
        /// <returns>The input types.</returns>
        public static ProcessorInput[] Inputs()
        {
            return new []
            {
                new ProcessorInput { Kind = ProcessorKinds.Nodes, Label = "Select Nodes to Move" },
                new ProcessorInput { Kind = ProcessorKinds.Node, Label = "Select Destination Node" }
            };
        }


        /// <summary>
        /// Processes the input data.
        /// </summary>
        /// <param name="inputs"></param>
        /// <returns>
        /// A string containing markup that is shown to the user.
        /// This will indicate success or failure.
        /// </returns>
        public static string ProcessInputs(List<object> inputs)
        {

            // Variables.
            var sources = inputs[0] as int[];
            var destination = (int)inputs[1];
            var service = ApplicationContext.Current.Services.ContentService;
            var destinationNode = service.GetById(destination);
            var removeEmpties = StringSplitOptions.RemoveEmptyEntries;
            var comma = ",".ToCharArray();
            var ancestors = destinationNode.Path.Split(comma, removeEmpties)
                .Select(x => int.Parse(x)).ToArray();


            // Ensure the destination node isn't under any of the source nodes.
            if (ancestors.Any(x => sources.Contains(x)))
            {
                return "<h2>Error</h2><br />The destination node cannot be one of or reside under any of the source nodes.";
            }


            // Move each node.
            foreach (var source in sources)
            {
                service.Move(service.GetById(source), destination);
            }


            // Refresh the XML cache.
            umbraco.library.RefreshContent();


            // Indicate success.
            return string.Format("<h2>Success</h2>Moved {0} nodes.", sources.Length.ToString());

        }

    }

    #endregion


    #region class BulkCreateNodes

    /// <summary>
    /// A data processor that creates multiple nodes under a specified parent node.
    /// </summary>
    [UmbracoDataProcessor(Label = "Bulk Create Nodes")]
    public static class BulkCreateNodes
    {

        /// <summary>
        /// The types of inputs required by this data processor.
        /// </summary>
        /// <returns>The input types.</returns>
        public static ProcessorInput[] Inputs()
        {
            return new []
            {
                new ProcessorInput { Kind = ProcessorKinds.Node, Label = "Select Parent Node" },
                new ProcessorInput { Kind = ProcessorKinds.Text, Label = "Delimiter Character (Default is Comma)" },
                new ProcessorInput { Kind = ProcessorKinds.Text, Label = "Node Names" },
                new ProcessorInput { Kind = ProcessorKinds.Text, Label = "Document Type Alias" }
            };
        }


        /// <summary>
        /// Processes the input data.
        /// </summary>
        /// <param name="inputs"></param>
        /// <returns>
        /// A string containing markup that is shown to the user.
        /// This will indicate success or failure.
        /// </returns>
        public static string ProcessInputs(List<object> inputs)
        {

            // Variables.
            var parent = (int)inputs[0];
            var delimiter = GetSplitDelimiter((string)inputs[1]);
            var strNames = (string)inputs[2];
            var doctypeAlias = (string)inputs[3];
            var service = ApplicationContext.Current.Services.ContentService;
            var removeEmpties = StringSplitOptions.RemoveEmptyEntries;


            // Get list of existing node names under parent.
            var existingNames = new HashSet<string>(
                service.GetChildren(parent).Select(x => x.Name.ToLower()));


            // Get names.
            var names = strNames.Split(delimiter, removeEmpties)
                .Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x))
                .DistinctBy(x => x.ToLower());
            var skippedNames = names.Where(x => existingNames.Contains(x.ToLower()));
            names = names.Where(x => !existingNames.Contains(x.ToLower()));


            // Create each node.
            foreach (var name in names)
            {
                service.Save(service.CreateContent(name, parent, doctypeAlias));
            }


            // Create a message with details about the operation.
            var li = "<li>{0}</li>";
            var encodedSkippedNames = skippedNames
                .Select(x => string.Format(li, WebUtility.HtmlEncode(x))).ToArray();
            var strSkipped = string.Join("", encodedSkippedNames);
            var encodedNames = names
                .Select(x => string.Format(li, WebUtility.HtmlEncode(x))).ToArray();
            var strCreated = string.Join("", encodedNames);
            var baseSkippedMessage = "<h3>Skipped these nodes:</h3><br><ul>{0}</ul><br>";
            var baseCreatedMessage = "<h3>Created these nodes:</h3><br><ul>{0}</ul><br>";
            var skippedMessage = skippedNames.Any()
                ? string.Format(baseSkippedMessage, strSkipped)
                : "<h3>No nodes skipped.</h3><br>";
            var createdMessage = names.Any()
                ? string.Format(baseCreatedMessage, strCreated)
                : "<h3>No nodes created.</h3><br>";
            var baseMessage = "<h2>Success</h2><br>{0}{1}";
            var message = string.Format(baseMessage, skippedMessage, createdMessage);


            // Indicate success.
            return message;

        }


        /// <summary>
        /// Gets a string array for the specified delimiter.
        /// This is useful for string split operations.
        /// </summary>
        /// <param name="delimiter">The delimiter.</param>
        /// <returns>The string array containing a delimiter.</returns>
        /// <remarks>
        /// If the specified delimiter is null/empty, comma will be used.
        /// </remarks>
        private static string[] GetSplitDelimiter(string delimiter)
        {
            if (string.IsNullOrEmpty(delimiter))
            {
                return new [] { "," };
            }
            else
            {
                return new [] { delimiter };
            }
        }

    }

    #endregion

}