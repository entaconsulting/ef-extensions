﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;

namespace Audit.Audit
{
    public abstract class AuditProfile 
    {
        private readonly IDictionary<Type, AuditConfiguration> _auditList;
//        private readonly IDictionary<Type, AuditConfiguration> _auditAllList;
        private readonly HashSet<Type> _excludeTypeList;
        private readonly HashSet<string> _excludeNsList;
        private bool _isInitialized;
        protected AuditProfile()
        {
            _auditList = new Dictionary<Type, AuditConfiguration>();
//            _auditAllList = new Dictionary<Type, AuditConfiguration>();
            _excludeTypeList = new HashSet<Type>();
            _excludeNsList = new HashSet<string>();
            _isInitialized = false;
        }

        public virtual void Configure()
        {
            _isInitialized = true;
        }

        protected IAuditHierarchyTableConfigurationExpression<T> AddAuditable<T>(Expression<Func<T, object>> uniqueId) where T : class
        {
            AuditConfiguration entityConfig;
            if (!_auditList.TryGetValue(typeof(T), out entityConfig))
            {
                entityConfig = new AuditConfiguration()
                {
                    AuditFields = new List<AuditFieldDefinition>(),
                    AuditReferences = new List<AuditConfigurationEntry.AuditConfigurationReferenceEntry>()
                };
                _auditList.Add(typeof(T), entityConfig);
            }
            if (uniqueId != null)
            {
                entityConfig.EntityKey = uniqueId;
                entityConfig.EntityKeyName = Helpers.GetFullPropertyName(uniqueId);
            }
            var configurationExpression = new AuditHierarchyConfigurationExpression<T>(this, typeof(T));
            return configurationExpression;
        }

        protected IAuditHierarchyTableConfigurationExpression<T> GetAuditable<T>() where T : class
        {
            if (!_auditList.ContainsKey(typeof(T)))
            {
                throw new InvalidOperationException(string.Format("Tipo '{0}' no mapeado",typeof(T).FullName));
            }
            var configurationExpression = new AuditHierarchyConfigurationExpression<T>(this, typeof(T));
            return configurationExpression;
        }

        protected IAuditHierarchyConfigurationExpression<T> AuditAllOfType<T>(Expression<Func<T, object>> uniqueId) where T : class
        {
            AuditConfiguration entityConfig;
            if (!_auditList.TryGetValue(typeof(T), out entityConfig))
            {
                entityConfig = new AuditConfiguration()
                {
                    AuditFields = new List<AuditFieldDefinition>(),
                    AuditReferences = new List<AuditConfigurationEntry.AuditConfigurationReferenceEntry>()
                };
                _auditList.Add(typeof(T), entityConfig);
            }
            if (uniqueId != null)
            {
                entityConfig.EntityKey = uniqueId;
                entityConfig.EntityKeyName = Helpers.GetFullPropertyName(uniqueId);
            }
            entityConfig.Generic = true;

            var configurationExpression = new AuditHierarchyConfigurationExpression<T>(this, typeof(T));
            return configurationExpression;

        }

        public void Exclude(Type exclude)
        {
            if (!_excludeTypeList.Contains(exclude))
            {
                _excludeTypeList.Add(exclude);
            }
        }

        public void ExcludeNameSpace(string ns)
        {
            if (!_excludeNsList.Contains(ns))
            {
                _excludeNsList.Add(ns);
            }
        }


        internal void AddAuditableField<T>(string fieldName, string fieldDescripcion, Dictionary<string, string> valuesConverter)
        {
            var entityConfig = _auditList[typeof(T)];
            entityConfig.AuditFields.Add(new AuditFieldDefinition() { FieldName = fieldName, FieldDescription = fieldDescripcion });
        }

        internal void AddAuditableReference<T, TRef>(string referenceCollectionName, string fieldName, string auditableFieldName, Type descriptionFieldType, string descriptionFieldName)
            where T : class
            where TRef : class
        {
            AuditConfiguration entityConfig;
            if (!_auditList.TryGetValue(typeof(T), out entityConfig))
            {
                AddAuditable<T>(null);
                entityConfig = _auditList[typeof(T)];
            }
            entityConfig.AuditReferences.Add(new AuditConfigurationEntry.AuditConfigurationReferenceEntry()
            {
                ReferenceCollectionName = referenceCollectionName,
                ReferencePropertyName = fieldName,
                DescriptionPropertyType = descriptionFieldType,
                DescriptionPropertyName = descriptionFieldName,
                AuditablePropertyName = auditableFieldName,
                ReferenceType = typeof(TRef)
            });

        }

        public AuditConfigurationEntry GetConfiguration(Type entityType)
        {
            if (!_isInitialized)
            {
                Configure();
            }

            var entry = new AuditConfigurationEntry() { IsAuditable = false };
            var configuration = GetConfigurationFromType(entityType);

            if (configuration == null)
                return entry;

            entry.IsAuditable = true;
            entry.EntityKey = configuration.EntityKey;
            entry.EntityKeyPropertyName = configuration.EntityKeyName;
            entry.AuditableFields = configuration.AuditFields;
            entry.AuditableReferences = configuration.AuditReferences;
            entry.CompositeKey = configuration.CompositeKeyFunc;
            entry.IgnoreIfNoFieldChanged = configuration.IgnoreIfNoFieldChanged;
            return entry;
        }

        private AuditConfiguration GetConfigurationFromType(Type entityType)
        {
            AuditConfiguration configuration;
            if (!_auditList.TryGetValue(entityType, out configuration))
            {
                //busco alguna configuración genérica para el tipo
                //me quedo con la más específica de las que hay
                var genericConfigurations = _auditList
                    .Where(a => a.Value.Generic &&  a.Key.IsAssignableFrom(entityType) && !_excludeTypeList.Contains(entityType) && !_excludeNsList.Contains(entityType.Namespace))
                    .ToList();
                if (genericConfigurations.Any())
                {
                    configuration = genericConfigurations
                        .Aggregate((pv, cv) => pv.Key.IsAssignableFrom(cv.Key) ? cv : pv)
                        .Value;
                }
            }
            return configuration;
        }

        public void AddCompositeKey<T>(Func<T, string> func) where T : class
        {
            _auditList[typeof(T)].CompositeKeyFunc = (o) => func(o as T);
        }

        public void SetIgnoreIfNoFieldChanged<T>()
        {
            _auditList[typeof (T)].IgnoreIfNoFieldChanged = true;
        }
    }
}