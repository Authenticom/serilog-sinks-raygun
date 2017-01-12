﻿// Copyright 2014 Serilog Contributors
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Mindscape.Raygun4Net;
using Mindscape.Raygun4Net.Builders;
using Mindscape.Raygun4Net.Messages;
using Serilog.Core;
using Serilog.Events;

namespace Serilog.Sinks.Raygun
{
    /// <summary>
    /// Writes log events to the Raygun.com service.
    /// </summary>
    public class RaygunSink : ILogEventSink
    {
        readonly IFormatProvider _formatProvider;
        readonly string _userNameProperty;
        readonly string _applicationVersionProperty;
        readonly IEnumerable<string> _tags;
        readonly IEnumerable<string> _ignoredFormFieldNames;
        readonly string _groupKeyProperty;
        readonly RaygunClient _client;

        /// <summary>
        /// Grouping event delegate
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="RaygunCustomGroupingKeyEventArgs"/> instance containing the event data.</param>
        /// <param name="group">The custom group object containing the custom group key</param>
        public delegate void Grouped(object sender, RaygunCustomGroupingKeyEventArgs e, IMessageGroup group);

        /// <summary>
        /// Occurs when the Raygun client fires the CustomGroupKey event.
        /// </summary>
        public event Grouped Grouping;

        /// <summary>
        /// Construct a sink that saves errors to the Raygun.io service. Properties are being send as userdata and the level is included as tag. The message is included inside the userdata.
        /// </summary>
        /// <param name="formatProvider">Supplies culture-specific formatting information, or null.</param>
        /// <param name="applicationKey">The application key as found on the Raygun website.</param>
        /// <param name="wrapperExceptions">If you have common outer exceptions that wrap a valuable inner exception which you'd prefer to group by, you can specify these by providing a list.</param>
        /// <param name="userNameProperty">Specifies the property name to read the username from. By default it is UserName. Set to null if you do not want to use this feature.</param>
        /// <param name="applicationVersionProperty">Specifies the property to use to retrieve the application version from. You can use an enricher to add the application version to all the log events. When you specify null, Raygun will use the assembly version.</param>
        /// <param name="tags">Specifies the tags to include with every log message. The log level will always be included as a tag.</param>
        /// <param name="ignoredFormFieldNames">Specifies the form field names which to ignore when including request form data.</param>
        /// <param name="groupKeyProperty">The property containing the custom group key for the Raygun message.</param>
        public RaygunSink(IFormatProvider formatProvider,
            string applicationKey,
            IEnumerable<Type> wrapperExceptions = null,
            string userNameProperty = "UserName",
            string applicationVersionProperty = "ApplicationVersion",
            IEnumerable<string> tags = null,
            IEnumerable<string> ignoredFormFieldNames = null,
            string groupKeyProperty = "GroupKey")
        {
            if (string.IsNullOrEmpty(applicationKey))
                throw new ArgumentNullException("applicationKey");

            _formatProvider = formatProvider;
            _userNameProperty = userNameProperty;
            _applicationVersionProperty = applicationVersionProperty;
            _tags = tags ?? new string[0];
            _ignoredFormFieldNames = ignoredFormFieldNames ?? Enumerable.Empty<string>();
            _groupKeyProperty = groupKeyProperty;

            _client = new RaygunClient(applicationKey);
            if (wrapperExceptions != null)
                _client.AddWrapperExceptions(wrapperExceptions.ToArray());

            // Wire up the CustomGroupingKey event
            _client.CustomGroupingKey += OnGrouping;
        }

        /// <summary>
        /// Emit the provided log event to the sink.
        /// </summary>
        /// <param name="logEvent">The log event to write.</param>
        public void Emit(LogEvent logEvent)
        {
            //Include the log level as a tag.
            var tags = _tags.Concat(new[] { logEvent.Level.ToString() }).ToList();

            var properties = logEvent.Properties
                         .Select(pv => new { Name = pv.Key, Value = RaygunPropertyFormatter.Simplify(pv.Value) })
                         .ToDictionary(a => a.Name, b => b.Value);

            // Add the message 
            properties.Add("RenderedLogMessage", logEvent.RenderMessage(_formatProvider));
            properties.Add("LogMessageTemplate", logEvent.MessageTemplate.Text);

            // Create new message
            var raygunMessage = new RaygunMessage
            {
                OccurredOn = logEvent.Timestamp.UtcDateTime
            };

            // Add exception when available
            if (logEvent.Exception != null)
                raygunMessage.Details.Error = RaygunErrorMessageBuilder.Build(logEvent.Exception);

            // Add user when requested
            if (!String.IsNullOrWhiteSpace(_userNameProperty) &&
                logEvent.Properties.ContainsKey(_userNameProperty) &&
                logEvent.Properties[_userNameProperty] != null)
            {
                raygunMessage.Details.User = new RaygunIdentifierMessage(logEvent.Properties[_userNameProperty].ToString());
            }

            // Add version when requested
            if (!String.IsNullOrWhiteSpace(_applicationVersionProperty) &&
                logEvent.Properties.ContainsKey(_applicationVersionProperty) &&
                logEvent.Properties[_applicationVersionProperty] != null)
            {
                raygunMessage.Details.Version = logEvent.Properties[_applicationVersionProperty].ToString();
            }

            // Build up the rest of the message
            raygunMessage.Details.Environment = new RaygunEnvironmentMessage();
            raygunMessage.Details.Tags = tags;
            raygunMessage.Details.UserCustomData = properties;
            raygunMessage.Details.MachineName = Environment.MachineName;

            if (HttpContext.Current != null)
            {
                // Request message is built here instead of raygunClient.Send so RequestMessageOptions have to be constructed here
                var requestMessageOptions = new RaygunRequestMessageOptions(_ignoredFormFieldNames, Enumerable.Empty<string>(), Enumerable.Empty<string>(), Enumerable.Empty<string>());
                raygunMessage.Details.Request = RaygunRequestMessageBuilder.Build(HttpContext.Current.Request, requestMessageOptions);
            }

            // Submit
            _client.Send(raygunMessage);
        }

        /// <summary>
        /// Even trigger for custom Grouping event
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="RaygunCustomGroupingKeyEventArgs" /> instance containing the event data.</param>
        protected virtual void OnGrouping(object sender, RaygunCustomGroupingKeyEventArgs e)
        {
            // search the custom data for the key
            var customKey = e.Message.Details.UserCustomData[_groupKeyProperty];
            if (customKey == null)
                return;

            // assign the custom key
            e.CustomGroupingKey = customKey.ToString();
            // invoke the custom event for unit testing
            Grouping?.Invoke(this, e, new MessageGroup {GroupKey = customKey.ToString()});
        }
    }
}
