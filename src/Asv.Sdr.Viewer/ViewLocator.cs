using System;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using ReactiveUI;

namespace Asv.Sdr.Viewer
{
    public class ViewLocator : IDataTemplate
    {
        private readonly CompositionContainer _container;

        public ViewLocator(CompositionContainer container)
        {
            _container = container;
        }

        public IControl Build(object data)
        {
            var viewModelType = data.GetType();
            var defaultView = _container.GetExports<IControl, IViewMetadata>().FirstOrDefault(_ => _.Metadata.ViewModelType == viewModelType);
            if (defaultView != null) return defaultView.Value;
            defaultView = _container.GetExports<IControl, IViewMetadata>().FirstOrDefault(_ => viewModelType.IsSubclassOf(_.Metadata.ViewModelType));
            if (defaultView != null) return defaultView.Value;
            // if have no attribute, just search view by name
            var name = viewModelType.FullName!.Replace("ViewModel", "View");
            var type = Type.GetType(name);
            if (type == null) return new TextBlock { Text = "Not Found: " + name };
            var contract = AttributedModelServices.GetContractName(type);
            return (IControl)_container.GetExportedValue<object>(contract)!;

        }

        public bool Match(object data)
        {
            return data is ReactiveObject;
        }
    }

    [MetadataAttribute]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class ExportViewAttribute : ExportAttribute, IViewMetadata
    {
        public ExportViewAttribute(Type viewModelType)
            : base(null, typeof(IControl))
        {
            this.ViewModelType = viewModelType;
        }

        public Type ViewModelType { get; private set; }
    }

    public interface IViewMetadata
    {
        Type ViewModelType { get; }
    }
}