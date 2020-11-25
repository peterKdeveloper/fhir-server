# FHIR Server for Azure

A .NET Core implementation of the FHIR standard.

| CI Build & Deployment | Azure Government Deployment |
|---|---|
| [![Build Status](https://microsofthealthoss.visualstudio.com/FhirServer/_apis/build/status/CI%20Build%20%26%20Deploy?branchName=master)](https://microsofthealthoss.visualstudio.com/FhirServer/_build/latest?definitionId=27&branchName=master) | [![Build Status](https://microsofthealthoss.visualstudio.com/FhirServer/_apis/build/status/CI%20Deployment%20MAG?branchName=master)](https://microsofthealthoss.visualstudio.com/FhirServer/_build/latest?definitionId=28&branchName=master)

FHIR Server for Azure is an open-source implementation of the emerging [HL7 Fast Healthcare Interoperability Resources (FHIR) specification](https://www.hl7.org/fhir/) designed for the Microsoft cloud. The FHIR specification defines how clinical health data can be made interoperable across systems, and the FHIR Server for Azure helps facilitate that interoperability in the cloud. The goal of this Microsoft Healthcare project is to enable developers to rapidly deploy a FHIR service.
 
With data in the FHIR format, the FHIR Server for Azure enables developers to quickly ingest and manage FHIR datasets in the cloud, track and manage data access and normalize data for machine learning workloads. FHIR Server for Azure is optimized for the Azure ecosystem: 
* Scripts and ARM templates are available for immediate provisioning in the Microsoft Cloud 
* Scripts are available to map to Azure AAD and enable role-based access control (RBAC) 

FHIR Server for Azure is built with logical separation, enabling developers with flexibility to modify how it is implemented, and extend its capabilities as needed. The logic layers of the FHIR server are:

* Hosting Layer – Supports hosting in different environments, with custom configuration of Inversion of Control (IoC) containers.
* RESTful API Layer – The implementation of the APIs defined by the HL7 FHIR specification.
* Core Logic Layer – The implementation of the core FHIR logic.
* Persistence Layer – A pluggable persistence provider enabling the FHIR server to connect to virtually any data persistence utility. FHIR Server for Azure includes a ready-to-use data persistence provider for Azure Cosmos DB (a globally replicated database service that offers rich querying over data).

FHIR Server for Azure empowers developers – saving time when they need to quickly integrate a FHIR server into their own applications or providing them with a foundation on which they can customize their own FHIR service. As an open source project, contributions and feedback from the FHIR developer community will continue to improve this project.

Privacy and security are top priorities and the FHIR Server for Azure has been developed in support of requirements for Protected Health Information (PHI). All the Azure services used in FHIR Server for Azure [meet the compliance requirements for Protected Health Information](https://www.microsoft.com/en-us/trustcenter/compliance/complianceofferings).

This open source project is fully backed by the Microsoft Healthcare team, but we know that this project will only get better with your feedback and contributions. We are leading the development of this code base, and test builds and deployments daily.

There is also a managed offering in Azure called the [Azure API for FHIR](https://azure.microsoft.com/services/azure-api-for-fhir/). This Platform as a Service (PaaS) FHIR server is backed by the open source project in this repository and it offers a turn key solution to provisioning a compliant, secure FHIR service.

# Release Notes
To see what is releasing in the FHIR Server, please refer to the [releases](https://github.com/microsoft/fhir-server/releases) section on GitHub. Starting in November 2020, we have tags on the PRs to better describe what is releasing. We have also released documentation on how to test the most recently build [here](docs/Testing-Releases.md). 

# Documentation

- Quickstart guides to deploy open source using [portal](docs/QuickstartDeployPortal.md), [CLI](docs/QuickstartDeployCLI.md), and [PowerShell](docs/QuickstartDeployPowershell.md).
- Register a [resource application](docs/Register-Resource-Application.md) and a [client application](docs/Register-Client-Application.md)
- [SMART on FHIR Proxy tutorial)(docs/SMARTonFHIR.md): Describes how to use the proxy to enable SMART on FHIR applications with the FHIR Server.
- [FHIR Postman tutorial](https://docs.microsoft.com/azure/healthcare-apis/access-fhir-postman-tutorial): Describes how to access a FHIR API using Postman.
- [Authentication](docs/Authentication.md): Describes the authentication settings for the FHIR server and how to make use of it in development and test scenarios.
- [Roles](docs/Roles.md): Describes how the FHIR Server for Azure role-based access control (RBAC) system works.
- [Search](docs/SearchArchitecture.md): Describes how search is implemented for the FHIR Server for Azure.
- [Debugging](docs/HowToDebug.md): Describes how to debug FHIR Server for Azure using Visual Studio.
- [Sql Schema Migration Guide](docs/SchemaMigrationGuide.md): Describes how to upgrade Schema for Sql Server.

## More Information

* [Azure API for FHIR documentation](https://docs.microsoft.com/azure/healthcare-apis/)
* [FHIR Server Samples Repo](https://github.com/Microsoft/fhir-server-samples): A demo sandbox using the Azure API for FHIR.
* [Health Architectures](https://aka.ms/healtharchitectures): A collection of reference architectures illustrating end-to-end best practices for using the Azure API for FHIR and related technologies.
* [FHIR Proxy](https://github.com/microsoft/health-architectures/tree/master/FHIR/FHIRProxy): Secure FHIR Gateway and Proxy to FHIR Servers.
* [Features](https://docs.microsoft.com/en-us/azure/healthcare-apis/fhir-features-supported): This document lists the main features of the FHIR Server for Azure and Azure API for FHIR.
* Blog: [FHIR Server for Azure, an open source project for modern healthcare](https://cloudblogs.microsoft.com/industry-blog/industry/health/fhir-server-for-azure-an-open-source-project-for-cloud-based-health-solutions/).
* Blog: [Azure API for FHIR moves to general availability](https://azure.microsoft.com/en-us/blog/azure-api-for-fhir-moves-to-general-availability/).
* Twitter: [Health_IT](https://twitter.com/Health_IT)

## Contributing
This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.microsoft.com.

When you submit a pull request, a CLA-bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., label, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

There are many other ways to contribute to FHIR Server for Azure.
* [Submit bugs](https://github.com/Microsoft/fhir-server/issues) and help us verify fixes as they are checked in.
* Review the [source code changes](https://github.com/Microsoft/fhir-server/pulls).
* Engage with FHIR Server for Azure users and developers on [StackOverflow](https://stackoverflow.com/questions/tagged/fhir-server-for-azure).
* Join the [#fhirforazure](https://twitter.com/hashtag/fhirserverforazure?f=tweets&vertical=default) discussion on Twitter.
* [Contribute bug fixes](CONTRIBUTING.md).

See [Contributing to FHIR Server for Azure](CONTRIBUTING.md) for more information.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

FHIR&reg; is the registered trademark of HL7 and is used with the permission of HL7. 
