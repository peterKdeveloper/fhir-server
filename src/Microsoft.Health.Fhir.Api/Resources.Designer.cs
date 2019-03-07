﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Microsoft.Health.Fhir.Api {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "15.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    public class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Microsoft.Health.Fhir.Api.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Back to top.
        /// </summary>
        public static string BackToTop {
            get {
                return ResourceManager.GetString("BackToTop", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The &quot;content-type&quot; header is required..
        /// </summary>
        public static string ContentTypeHeaderRequired {
            get {
                return ResourceManager.GetString("ContentTypeHeaderRequired", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Authorization failed..
        /// </summary>
        public static string Forbidden {
            get {
                return ResourceManager.GetString("Forbidden", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to There was an error processing your request..
        /// </summary>
        public static string GeneralInternalError {
            get {
                return ResourceManager.GetString("GeneralInternalError", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to API.
        /// </summary>
        public static string MenuAPI {
            get {
                return ResourceManager.GetString("MenuAPI", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Capability Statement.
        /// </summary>
        public static string MenuCapabilityStatement {
            get {
                return ResourceManager.GetString("MenuCapabilityStatement", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The audit information is missing for Controller: {0} and Action: {1}. This usually means the action is not marked with appropriate attribute..
        /// </summary>
        public static string MissingAuditInformation {
            get {
                return ResourceManager.GetString("MissingAuditInformation", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The requested route was not found..
        /// </summary>
        public static string NotFoundException {
            get {
                return ResourceManager.GetString("NotFoundException", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to FHIR Server.
        /// </summary>
        public static string PageTitle {
            get {
                return ResourceManager.GetString("PageTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The security configuration requires the authority to be set to an https address..
        /// </summary>
        public static string RequireHttpsMetadataError {
            get {
                return ResourceManager.GetString("RequireHttpsMetadataError", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Both id and the resource are required..
        /// </summary>
        public static string ResourceAndIdRequired {
            get {
                return ResourceManager.GetString("ResourceAndIdRequired", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Id must be specified in the resource..
        /// </summary>
        public static string ResourceIdRequired {
            get {
                return ResourceManager.GetString("ResourceIdRequired", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Resource type in the URL must match resourceType in the resource..
        /// </summary>
        public static string ResourceTypeMismatch {
            get {
                return ResourceManager.GetString("ResourceTypeMismatch", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Toggle navigation.
        /// </summary>
        public static string ToggleNavigation {
            get {
                return ResourceManager.GetString("ToggleNavigation", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Unable to obtain OpenID configuration..
        /// </summary>
        public static string UnableToObtainOpenIdConfiguration {
            get {
                return ResourceManager.GetString("UnableToObtainOpenIdConfiguration", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Authentication failed..
        /// </summary>
        public static string Unauthorized {
            get {
                return ResourceManager.GetString("Unauthorized", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The requested &quot;accept&quot; header is not supported..
        /// </summary>
        public static string UnsupportedAcceptHeader {
            get {
                return ResourceManager.GetString("UnsupportedAcceptHeader", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The requested &quot;content-type&quot; header is not supported..
        /// </summary>
        public static string UnsupportedContentTypeHeader {
            get {
                return ResourceManager.GetString("UnsupportedContentTypeHeader", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The requested &quot;_format&quot; parameter is not supported..
        /// </summary>
        public static string UnsupportedFormatParameter {
            get {
                return ResourceManager.GetString("UnsupportedFormatParameter", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The requested &quot;prefer&quot; header is not supported..
        /// </summary>
        public static string UnsupportedPreferHeader {
            get {
                return ResourceManager.GetString("UnsupportedPreferHeader", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Id in the URL must match id in the resource..
        /// </summary>
        public static string UrlResourceIdMismatch {
            get {
                return ResourceManager.GetString("UrlResourceIdMismatch", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to View was not found..
        /// </summary>
        public static string ViewNotFound {
            get {
                return ResourceManager.GetString("ViewNotFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to STU3 API &lt;small&gt;preview&lt;/small&gt;.
        /// </summary>
        public static string WelcomeTitle {
            get {
                return ResourceManager.GetString("WelcomeTitle", resourceCulture);
            }
        }
    }
}
