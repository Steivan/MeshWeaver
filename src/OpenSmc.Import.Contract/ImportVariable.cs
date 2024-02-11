﻿using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using OpenSmc.Activities;
using OpenSmc.Data;
using OpenSmc.DataSetReader;
using OpenSmc.DataStructures;
using OpenSmc.FileStorage;
using OpenSmc.Import.Builders;
using OpenSmc.Import.Mapping;
using OpenSmc.Import.Options;

namespace OpenSmc.Import
{
    public class ImportVariable : IImportVariable
    {
        private readonly IActivityService activityService;
        private readonly IMappingService mappingService;
        private readonly IServiceProvider serviceProvider;

        private IFileReadStorage fileReadStorage;
        private IWorkspace targetSource;
        private ImmutableList<Func<object, ValidationContext, Task<bool>>> defaultValidations = ImmutableList<Func<object, ValidationContext, Task<bool>>>.Empty;
        private readonly Dictionary<string, Func<ImportOptions, IDataSet, Task>> importFormatFunctions = new();

        public ImportVariable(IActivityService activityService, 
                              IDataSetReaderVariable dataSetReaderVariable,
                              IMappingService mappingService,
                              IServiceProvider serviceProvider)
        {
            this.activityService = activityService;
            this.mappingService = mappingService;
            this.serviceProvider = serviceProvider;
        }
        public void SetDefaultFileStorage(IFileReadStorage storage)
        {
            fileReadStorage = storage;
        }


        public void SetDefaultValidation(Func<object, ValidationContext, bool> validationRule)
        {
            SetDefaultValidation((obj, vc) =>
                                 {
                                     validationRule ??= (_, _) => true;
                                     var ret = validationRule(obj, vc);
                                     return Task.FromResult(ret);
                                 });
        }

        public void SetDefaultValidation(Func<object, ValidationContext, Task<bool>> validationRule)
        {
            validationRule ??= (_, _) => Task.FromResult(true);
            defaultValidations = defaultValidations.Add(validationRule);
        }

        public void DefineFormat(string format, Func<ImportOptions, IDataSet, Task> importFunction)
        {
            if (format == null)
                throw new ArgumentNullException($"{nameof(format)}");

            importFormatFunctions[format] = importFunction ?? throw new ArgumentNullException($"{nameof(importFunction)}");
        }

        public void SetDefaultTarget(IWorkspace target)
        {
            targetSource = target;
        }

        public FileReaderImportOptionsBuilder FromFile(string filePath)
        {
            return new(activityService, 
                       mappingService,
                       fileReadStorage,
                       CancellationToken.None,
                       targetSource,
                       serviceProvider,
                       importFormatFunctions,
                       defaultValidations,
                       filePath);
        }

        public StringImportOptionsBuilder FromString(string content)
        {
            return new(activityService,
                       mappingService,
                       fileReadStorage,
                       CancellationToken.None,
                       targetSource,
                       serviceProvider,
                       importFormatFunctions,
                       defaultValidations,
                       content);
        }

        public StreamImportOptionsBuilder FromStream(Stream stream)
        {
            return new(activityService,
                       mappingService,
                       fileReadStorage,
                       CancellationToken.None,
                       targetSource,
                       serviceProvider,
                       importFormatFunctions,
                       defaultValidations,
                       stream);
        }

        public DataSetImportOptionsBuilder FromDataSet(IDataSet dataSet)
        {
            return new(activityService,
                       mappingService,
                       fileReadStorage,
                       CancellationToken.None,
                       targetSource,
                       serviceProvider,
                       importFormatFunctions,
                       defaultValidations,
                       dataSet);
        }
    }
}
