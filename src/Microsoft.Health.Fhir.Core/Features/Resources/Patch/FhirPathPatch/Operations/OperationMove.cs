using System;
using System.Linq;
using FhirPathPatch.Helpers;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;
using Hl7.Fhir.Specification;
using static Hl7.Fhir.Model.Parameters;

namespace FhirPathPatch.Operations
{
    public class OperationMove : OperationBase, IOperation
    {
        public OperationMove(Resource resource)
            : base(resource) { }

        /// <summary>
        /// Executes a FHIRPath Patch Move operation. Move operations will
        /// move an existing element inside a list at the specified
        /// operation.Path from the index of operation.Source to the index of
        /// operation.Destination.
        ///
        /// Fhir package does NOT have a built-in operation which accomplishes
        /// this. So we must inspect the existing list and recreate it with the
        /// correct elements in order.
        /// </summary>
        /// <param name="operation">PendingOperation representing Move operation.</param>
        /// <returns>Patched FHIR Resource as POCO.</returns>
        public override Resource Execute(PendingOperation operation)
        {
            // Setup
            var targetElement = this.ResourceElement.Find(operation.Path);
            var targetParent = this.ResourceElement.Parent;
            var name = this.ResourceElement.Name;

            // Remove specified element from the list
            var elementToMove = targetParent.AtIndex(name, operation.Source ?? -1);
            if (!targetParent.Remove(elementToMove))
                throw new InvalidOperationException();

            // There is no easy "move" operation in the FHIR library, so we must
            // iterate over the list to reconstruct it.
            foreach (var child in targetParent.Children(name).ToList()
                                            .Select(x => x as ElementNode)
                                            .Select((value, index) => (value, index)))
            {
                // Add the new item at the correct index
                if (operation.Destination == child.index)
                    targetParent.Add(this.PocoProvider, operation.Value.ToElementNode(), name);

                // Remove the old element from the list so the new order is used
                if (!targetParent.Remove(child.value))
                    throw new InvalidOperationException();

                // Add the old element back to the list
                targetParent.Add(this.PocoProvider, child.value, name);
            }

            return this.ResourceElement.ToPoco<Resource>();
        }
    }
}
