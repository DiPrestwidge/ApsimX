﻿// -----------------------------------------------------------------------
// <copyright file="Report.cs" company="APSIM Initiative">
//     Copyright (c) APSIM Initiative
// </copyright>
//-----------------------------------------------------------------------
namespace Models.Report
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using Models.Core;
    using APSIM.Shared.Utilities;
    using Factorial;
    using System.Xml.Serialization;

    /// <summary>
    /// A report class for writing output to the data store.
    /// </summary>
    [Serializable]
    [ViewName("UserInterface.Views.ReportView")]
    [PresenterName("UserInterface.Presenters.ReportPresenter")]
    [ValidParent(ParentType = typeof(Zone))]
    [ValidParent(ParentType = typeof(Zones.CircularZone))]
    [ValidParent(ParentType = typeof(Zones.RectangularZone))]
    public class Report : Model
    {
        /// <summary>
        /// The columns to write to the data store.
        /// </summary>
        private List<ReportColumn> columns = null;

        /// <summary>
        /// A reference to the simulation
        /// </summary>
        [Link]
        private Simulation simulation = null;

        /// <summary>Experiment factor names</summary>
        public List<string> ExperimentFactorNames { get; set; }

        /// <summary>Experiment factor values</summary>
        public List<string> ExperimentFactorValues { get; set; }

        /// <summary>
        /// Gets or sets variable names for outputting
        /// </summary>
        [Summary]
        [Description("Output variables")]
        public string[] VariableNames { get; set; }

        /// <summary>
        /// Gets or sets event names for outputting
        /// </summary>
        [Summary]
        [Description("Output frequency")]
        public string[] EventNames { get; set; }

        /// <summary>An event handler to allow us to initialize ourselves.</summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event arguments</param>
        [EventSubscribe("Commencing")]
        private void OnSimulationCommencing(object sender, EventArgs e)
        {
            List<string> eventNames = new List<string>();
            for (int i = 0; i < this.EventNames.Length; i++)
            {
                if (this.EventNames[i] != string.Empty)
                    eventNames.Add(this.EventNames[i].Trim());
            }

            this.EventNames = eventNames.ToArray();

            // sanitise the variable names and remove duplicates
            List<string> variableNames = new List<string>();
            variableNames.Add("Name as ZoneName");
            for (int i = 0; i < this.VariableNames.Length; i++)
            {
                bool isDuplicate = StringUtilities.IndexOfCaseInsensitive(variableNames, this.VariableNames[i].Trim()) != -1;
                if (!isDuplicate && this.VariableNames[i] != string.Empty)
                    variableNames.Add(this.VariableNames[i].Trim());
            }
            this.VariableNames = variableNames.ToArray();
            this.FindVariableMembers();
        }

        /// <summary>A method that can be called by other models to perform a line of output.</summary>
        public void DoOutput()
        {
            foreach (ReportColumn column in columns)
                column.StoreValue();
        }

        /// <summary>
        /// Fill the Members list with VariableMember objects for each variable.
        /// </summary>
        private void FindVariableMembers()
        {
            this.columns = new List<ReportColumn>();

            AddExperimentFactorLevels();

            foreach (string fullVariableName in this.VariableNames)
            {
                if (fullVariableName != string.Empty)
                    this.columns.Add(ReportColumn.Create(fullVariableName, this, this.EventNames));
            }
        }

        /// <summary>Add the experiment factor levels as columns.</summary>
        private void AddExperimentFactorLevels()
        {
            if (ExperimentFactorValues != null)
            {
                for (int i = 0; i < ExperimentFactorNames.Count; i++)
                    this.columns.Add(new ReportColumnConstantValue(ExperimentFactorNames[i], ExperimentFactorNames[i], this.EventNames, this, ExperimentFactorValues[i]));
            }
        }

        /// <summary>
        /// Simulation has completed - write the report table.
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event arguments</param>
        [EventSubscribe("Completed")]
        private void OnSimulationCompleted(object sender, EventArgs e)
        {
            // Get rid of old data in .db
            DataStore dataStore = new DataStore(this);
            if (this.simulation != null)
            {
                dataStore.DeleteOldContentInTable(this.simulation.Name, this.Name);
            }
            // Write and store a table in the DataStore
            if (this.columns != null && this.columns.Count > 0)
            {
                DataTable table = new DataTable();

                foreach (ReportColumn variable in this.columns)
                    variable.AddColumnsToTable(table);

                foreach (ReportColumn variable in this.columns)
                    variable.AddRowsToTable(table);

                dataStore.WriteTable(this.simulation.Name, this.Name, table);

                this.columns.Clear();
                this.columns = null;
            }
            
            dataStore.Disconnect();
            dataStore = null;
        }
    }
}