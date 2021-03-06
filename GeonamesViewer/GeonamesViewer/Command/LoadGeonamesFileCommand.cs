﻿using Esri.ArcGISRuntime.UI;
using GeonamesViewer.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace GeonamesViewer.Command
{
    /// <summary>
    /// Loads a geonames file into the specified graphics overlay.
    /// </summary>
    internal class LoadGeonamesFileCommand : ICommand
    {
        private readonly GeonamesOverlay _overlay;

        internal LoadGeonamesFileCommand(GeonamesOverlay overlay)
        {
            _overlay = overlay;
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter)
        {
            var filePaths = parameter as string[];
            if (null == filePaths)
            {
                return false;
            }

            foreach (var filePath in filePaths)
            {
                if (!File.Exists(filePath))
                {
                    return false;
                }
            }
            return true;
        }

        public void Execute(object parameter)
        {
            if (!CanExecute(parameter))
            {
                throw new ArgumentException(string.Format(@"{0} is not a valid parameter!", parameter));
            }

            // Get the UI scheduler and start a new task for reading the file
            var filePaths = (string[]) parameter;
            var uiScheduler = TaskScheduler.FromCurrentSynchronizationContext();
            Task.Factory.StartNew(async () =>
            {
                foreach (var filePath in filePaths)
                {
                    using (var reader = new StreamReader(filePath))
                    {
                        string line;
                        const int bufferSize = 10000;
                        var records = new List<GeonamesRecord>(bufferSize);
                        for (var recordIndex = 0; null != (line = reader.ReadLine()); recordIndex++)
                        {
                            var tokens = line.Split('\t');
                            if (4 < tokens.Length)
                            {
                                var record = new GeonamesRecord();
                                record.GeonamesId = tokens[0];
                                record.Name = tokens[1];
                                var latitude = tokens[4];
                                var longitude = tokens[5];
                                double x, y;
                                if (double.TryParse(longitude, NumberStyles.Any, CultureInfo.InvariantCulture, out x)
                                    && double.TryParse(latitude, NumberStyles.Any, CultureInfo.InvariantCulture, out y))
                                {
                                    record.Latitude = y;
                                    record.Longitude = x;
                                    records.Add(record);

                                    if (0 == records.Count % bufferSize)
                                    {
                                        // Ensure the graphics are added using the UI scheduler
                                        await AddRecordsAsync(records, uiScheduler);
                                        records.Clear();
                                    }
                                }
                            }
                        }

                        if (0 < records.Count)
                        {
                            // Ensure the graphics are added using the UI scheduler
                            await AddRecordsAsync(records, uiScheduler);
                            records.Clear();
                        }
                    }
                }
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Current);
        }

        private async Task AddRecordsAsync(IList<GeonamesRecord> records, TaskScheduler uiScheduler)
        {
            // Create a copy of records
            var recordsCopy = new List<GeonamesRecord>(records);
            await Task.Factory.StartNew(async () =>
            {
                foreach (var recordCopy in recordsCopy)
                {
                    await _overlay.AddRecordAsync(recordCopy);
                }
            }, CancellationToken.None, TaskCreationOptions.None, uiScheduler);
        }
    }
}
