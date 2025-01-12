﻿using System.IO;
using System.Xml;
using Models.Core;
using System.Xml.Serialization;
using System;
using System.Reflection;
using System.Collections.Generic;
using Models.Factorial;
using APSIM.Shared.Utilities;
using System.Linq;

namespace Models.Core
{
    /// <summary>
    /// Encapsulates a collection of simulations. It is responsible for creating this collection,
    /// changing the structure of the components within the simulations, renaming components, adding
    /// new ones, deleting components. The user interface talks to an instance of this class.
    /// </summary>
    [Serializable]
    public class Simulations : Model
    {
        /// <summary>The _ file name</summary>
        private string _FileName;

        /// <summary>Gets or sets the width of the explorer.</summary>
        /// <value>The width of the explorer.</value>
        public Int32 ExplorerWidth { get; set; }

        /// <summary>Gets or sets the version.</summary>
        [XmlAttribute("Version")]
        public int Version { get; set; }

        /// <summary>Gets a value indicating whether this job is completed. Set by JobManager.</summary>
        [XmlIgnore]
        public bool IsCompleted { get; set; }

        /// <summary>Gets the error message. Can be null if no error. Set by JobManager.</summary>
        [XmlIgnore]
        public string ErrorMessage { get; set; }

        /// <summary>Gets a value indicating whether this instance is computationally time consuming.</summary>
        public bool IsComputationallyTimeConsuming { get { return false; } }

        /// <summary>The name of the file containing the simulations.</summary>
        /// <value>The name of the file.</value>
        [XmlIgnore]
        public string FileName
        {
            get
            {
                return _FileName;
            }
            set
            {
                _FileName = value;
            }
        }

        /// <summary>
        /// A list of all exceptions thrown during the creation and loading of the simulation.
        /// </summary>
        /// <value>The load errors.</value>
        [XmlIgnore]
        public List<Exception> LoadErrors { get; private set; }

        /// <summary>Create a simulations object by reading the specified filename</summary>
        /// <param name="FileName">Name of the file.</param>
        /// <returns></returns>
        /// <exception cref="System.Exception">Simulations.Read() failed. Invalid simulation file.\n</exception>
        public static Simulations Read(string FileName)
        {
            // Run the converter.
            Converter.ConvertToLatestVersion(FileName);

            // Deserialise
            Simulations simulations = XmlUtilities.Deserialise(FileName, Assembly.GetExecutingAssembly()) as Simulations;

            if (simulations != null)
            {
                // Set the filename
                simulations.FileName = FileName;
                simulations.SetFileNameInAllSimulations();

                // Call the OnDeserialised method in each model.
                object[] args = new object[] { true };
                foreach (Model model in Apsim.ChildrenRecursively(simulations))
                    Apsim.CallEventHandler(model, "Deserialised", args);

                // Parent all models.
                simulations.Parent = null;
                Apsim.ParentAllChildren(simulations);

                // Call OnLoaded in all models.
                simulations.LoadErrors = new List<Exception>();
                foreach (Model child in Apsim.ChildrenRecursively(simulations))
                {
                    try
                    {
                        Apsim.CallEventHandler(child, "Loaded", null);
                    }
                    catch (ApsimXException err)
                    {
                        simulations.LoadErrors.Add(err);
                    }
                    catch (Exception err)
                    {
                        err.Source = child.Name;
                        simulations.LoadErrors.Add(err);
                    }
                }
            }

            return simulations;
        }

        /// <summary>Create a simulations object by reading the specified filename</summary>
        /// <param name="node">The node.</param>
        /// <returns></returns>
        /// <exception cref="System.Exception">Simulations.Read() failed. Invalid simulation file.\n</exception>
        public static Simulations Read(XmlNode node)
        {
            // Run the converter.
            Converter.ConvertToLatestVersion(node);

            // Deserialise
            Simulations simulations = XmlUtilities.Deserialise(node, Assembly.GetExecutingAssembly()) as Simulations;

            if (simulations != null)
            {
                // Set the filename
                simulations.SetFileNameInAllSimulations();

                // Call the OnSerialised method in each model.
                object[] args = new object[] { true };
                foreach (Model model in Apsim.ChildrenRecursively(simulations))
                    Apsim.CallEventHandler(model, "Deserialised", args);

                // Parent all models.
                simulations.Parent = null;
                Apsim.ParentAllChildren(simulations);

                CallOnLoaded(simulations);
            }
            else
                throw new Exception("Simulations.Read() failed. Invalid simulation file.\n");
            return simulations;
        }

        /// <summary>Call Loaded event in specified model and all children</summary>
        /// <param name="model">The model.</param>
        public static void CallOnLoaded(IModel model)
        {
            // Call OnLoaded in all models.
            Apsim.CallEventHandler(model, "Loaded", null);
            foreach (Model child in Apsim.ChildrenRecursively(model))
                Apsim.CallEventHandler(child, "Loaded", null);
        }

        /// <summary>Write the specified simulation set to the specified filename</summary>
        /// <param name="FileName">Name of the file.</param>
        public void Write(string FileName)
        {
            string tempFileName = Path.Combine(Path.GetTempPath(), Path.GetFileName(FileName));
            StreamWriter Out = new StreamWriter(tempFileName);
            Write(Out);
            Out.Close();

            // If we get this far without an exception then copy the tempfilename over our filename,
            // creating a backup (.bak) in the process.
            string bakFileName = FileName + ".bak";
            File.Delete(bakFileName);
            if (File.Exists(FileName))
                File.Move(FileName, bakFileName);
            File.Move(tempFileName, FileName);
            this.FileName = FileName;
            SetFileNameInAllSimulations();
        }

        /// <summary>Write the specified simulation set to the specified 'stream'</summary>
        /// <param name="stream">The stream.</param>
        public void Write(TextWriter stream)
        {
            object[] args = new object[] { true };
            foreach (Model model in Apsim.ChildrenRecursively(this))
                Apsim.CallEventHandler(model, "Serialising", args);

            try
            {
                stream.Write(XmlUtilities.Serialise(this, true));
            }
            finally
            {
                foreach (Model model in Apsim.ChildrenRecursively(this))
                    Apsim.CallEventHandler(model, "Serialised", args);

            }
        }

        /// <summary>Constructor, private to stop developers using it. Use Simulations.Read instead.</summary>
        private Simulations() { }

        /// <summary>Find all simulations under the specified parent model.</summary>
        /// <param name="parent">The parent.</param>
        /// <returns></returns>
        public static Simulation[] FindAllSimulationsToRun(Model parent)
        {
            List<Simulation> simulations = new List<Simulation>();

            if (parent is Experiment)
                simulations.AddRange((parent as Experiment).Create());
            else if (parent is Simulation)
            {
                Simulation clonedSim = Apsim.Clone(parent) as Simulation;
                CallOnLoaded(clonedSim);
                simulations.Add(clonedSim);
            }
            else
            {
                // Look for simulations.
                foreach (Model model in Apsim.ChildrenRecursively(parent))
                {
                    if (model is Experiment)
                        simulations.AddRange((model as Experiment).Create());
                    else if (model is Simulation && !(model.Parent is Experiment))
                    {
                        Simulation clonedSim = Apsim.Clone(model) as Simulation;
                        CallOnLoaded(clonedSim);
                        simulations.Add(clonedSim);
                    }
                }
            }
            // Make sure each simulation has it's filename set correctly.
            foreach (Simulation simulation in simulations)
            {
                if (simulation.FileName == null)
                    simulation.FileName = RootSimulations(parent).FileName;
            }
            return simulations.ToArray();
        }

        /// <summary>Find all simulation names that are going to be run.</summary>
        /// <returns></returns>
        public string[] FindAllSimulationNames()
        {
            List<string> simulations = new List<string>();
            // Look for simulations.
            foreach (Model Model in Apsim.ChildrenRecursively(this))
            {
                if (Model is Simulation)
                {
                    // An experiment can have a base simulation - don't return that to caller.
                    if (!(Model.Parent is Experiment))
                        simulations.Add(Model.Name);
                }
            }

            // Look for experiments and get them to create their simulations.
            foreach (Model experiment in Apsim.ChildrenRecursively(this))
            {
                if (experiment is Experiment)
                    simulations.AddRange((experiment as Experiment).Names());
            }

            return simulations.ToArray();

        }

        /// <summary>Look through all models. For each simulation found set the filename.</summary>
        private void SetFileNameInAllSimulations()
        {
            foreach (Model simulation in Apsim.ChildrenRecursively(this))
                if (simulation is Simulation)
                    (simulation as Simulation).FileName = FileName;
        }

        /// <summary>Roots the simulations.</summary>
        /// <param name="model">The model.</param>
        /// <returns></returns>
        private static Simulations RootSimulations(Model model)
        {
            Model m = model;
            while (m != null && m.Parent != null && !(m is Simulations))
                m = m.Parent as Model;

            return m as Simulations;
        }

        /// <summary>Make model substitutions if necessary.</summary>
        /// <param name="simulations">The simulations to make substitutions in.</param>
        public void MakeSubstitutions(Simulation[] simulations)
        {
            IModel replacements = Apsim.Child(this, "Replacements");
            if (replacements != null)
            {
                foreach (IModel replacement in replacements.Children)
                {
                    foreach (Simulation simulation in simulations)
                    {
                        foreach (IModel match in Apsim.FindAll(simulation, replacement.GetType()))
                        {
                            if (match.Name.Equals(replacement.Name, StringComparison.InvariantCultureIgnoreCase))
                            {
                                // Do replacement.
                                IModel newModel = Apsim.Clone(replacement);
                                int index = match.Parent.Children.IndexOf(match as Model);
                                match.Parent.Children.Insert(index, newModel as Model);
                                newModel.Parent = match.Parent;
                                match.Parent.Children.Remove(match as Model);
                                CallOnLoaded(newModel);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>Documents the specified model.</summary>
        /// <param name="modelNameToDocument">The model name to document.</param>
        /// <param name="tags">The auto doc tags.</param>
        /// <param name="headingLevel">The starting heading level.</param>
        public void DocumentModel(string modelNameToDocument, List<AutoDocumentation.ITag> tags, int headingLevel)
        {
            Simulation simulation = Apsim.Find(this, typeof(Simulation)) as Simulation;
            if (simulation != null)
            {
                // Find the model of the right name.
                IModel modelToDocument = Apsim.Find(simulation, modelNameToDocument);

                // If not found then find a model of the specified type.
                if (modelToDocument == null)
                    modelToDocument = Apsim.Get(simulation, "[" + modelNameToDocument + "]") as IModel;

                // If still not found throw an error.
                if (modelToDocument == null)
                    throw new ApsimXException(this, "Could not find a model of the name " + modelNameToDocument + ". Simulation file name must match the name of the node to document.");

                // Get the path of the model (relative to parentSimulation) to document so that 
                // when replacements happen below we will point to the replacement model not the 
                // one passed into this method.
                string pathOfSimulation = Apsim.FullPath(simulation) + ".";
                string pathOfModelToDocument = Apsim.FullPath(modelToDocument).Replace(pathOfSimulation, "");

                // Clone the simulation
                Simulation clonedSimulation = Apsim.Clone(simulation) as Simulation;

                // Make any substitutions.
                MakeSubstitutions(new Simulation[] { clonedSimulation });

                // Now use the path to get the model we want to document.
                modelToDocument = Apsim.Get(clonedSimulation, pathOfModelToDocument) as IModel;

                // resolve all links in cloned simulation.
                Apsim.ResolveLinks(clonedSimulation);
                foreach (Model child in Apsim.ChildrenRecursively(clonedSimulation))
                    Apsim.ResolveLinks(child);

                // Document the model.
                modelToDocument.Document(tags, headingLevel, 0);

                // Unresolve links.
                Apsim.UnresolveLinks(clonedSimulation);
                foreach (Model child in Apsim.ChildrenRecursively(clonedSimulation))
                    Apsim.UnresolveLinks(child);
            }
        }
    }
}
