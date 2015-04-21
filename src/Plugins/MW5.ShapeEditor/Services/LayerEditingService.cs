﻿using System;
using System.Windows.Forms;
using MW5.Api;
using MW5.Api.Concrete;
using MW5.Api.Enums;
using MW5.Api.Helpers;
using MW5.Api.Interfaces;
using MW5.Api.Legend;
using MW5.Plugins.Interfaces;
using MW5.Plugins.Services;
using MW5.Services.Views;
using MW5.Shared;
using MW5.UI.Helpers;

namespace MW5.Plugins.ShapeEditor.Services
{
    public class LayerEditingService: ILayerEditingService
    {
        private readonly IAppContext _context;

        public LayerEditingService(IAppContext context)
        {
            if (context == null) throw new ArgumentNullException("context");
            _context = context;
        }

        /// <summary>
        /// Toggles interactive editing state for vector layer.
        /// </summary>
        public void ToggleVectorLayerEditing()
        {
            // perhaps better to allow setting state explicitly as parameter
            int handle = _context.Legend.SelectedLayerHandle;
            var fs = _context.Layers.GetFeatureSet(handle);
            var ogrLayer = _context.Layers.GetVectorLayer(handle);

            if (fs != null)
            {
                if (fs.InteractiveEditing)
                {
                    SaveLayerChanges(handle);
                }
                else
                {
                    if (ogrLayer != null)
                    {
                        if (ogrLayer.DynamicLoading)
                        {
                            MessageService.Current.Info("Editing of dynamically loaded OGR layers isn't allowed.");
                            return;
                        }
                        if (!ogrLayer.get_SupportsEditing(SaveType.SaveAll))
                        {
                            MessageService.Current.Info("OGR layer doesn't support editing: " + ogrLayer.LastError);
                            return;
                        }
                    }
                    fs.InteractiveEditing = true;
                }

                _context.Legend.Redraw(LegendRedraw.LegendAndMap);
            }
        }

        /// <summary>
        /// Saves changes for the layer with specified handle.
        /// </summary>
        public bool SaveLayerChanges(int layerHandle)
        {
            string layerName = _context.Layers.ItemByHandle(layerHandle).Name;

            string prompt = string.Format("Save changes for the layer: {0}?", layerName);
            var result = MessageService.Current.AskWithCancel(prompt);

            switch (result)
            {
                case DialogResult.Yes:
                    return SaveChangesCore(layerHandle);
                case DialogResult.No:
                    DiscardChangesCore(layerHandle);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Saves changes for shapefile or OGR layer.
        /// </summary>
        private bool SaveChangesCore(int layerHandle)
        {
            var sf = _context.Layers.GetFeatureSet(layerHandle);
            var ogrLayer = _context.Layers.GetVectorLayer(layerHandle);

            bool success;

            if (ogrLayer != null)
            {
                int savedCount;
                SaveResult saveResult = ogrLayer.SaveChanges(out savedCount);
                success = saveResult == SaveResult.AllSaved || saveResult == SaveResult.NoChanges;
                
                if (!success)
                {
                    Logger.Current.Warn("Failed to save OGR layer changes: " + ogrLayer.Filename);
                    DisplayOgrErrors(ogrLayer);
                }

                string msg = string.Format("{0}: {1}; features: {2}", saveResult.EnumToString(), ogrLayer.Name, savedCount);
                MessageService.Current.Info(msg);
            }
            else
            {
                success = sf.StopEditingShapes();
            }

            if (success)
            {
                CloseEditing(layerHandle);
            }

            return success;
        }

        /// <summary>
        /// Discards changes for the layer.
        /// </summary>
        private void DiscardChangesCore(int layerHandle)
        {
            var ogrLayer = _context.Layers.GetVectorLayer(layerHandle);
            
            if (ogrLayer != null)
            {
                ogrLayer.ReloadFromSource();
            }
            else
            {
                var sf = _context.Layers.GetFeatureSet(layerHandle);
                sf.StopEditingShapes(false, true);
            }
            
            CloseEditing(layerHandle);
        }

        /// <summary>
        /// Closes editing session for layer (changes should be saved or discarded prior to this call).
        /// </summary>
        private void CloseEditing(int layerHandle)
        {
            var sf = _context.Layers.GetFeatureSet(layerHandle);
            sf.InteractiveEditing = false;
            _context.Map.GeometryEditor.Clear();
            _context.Map.History.ClearForLayer(layerHandle);
            _context.Map.Redraw();
        }

        private void DisplayOgrErrors(IVectorLayer layer)
        {
            var logger = Logger.Current;
            for (int i = 0; i < layer.UpdateSourceErrorCount; i++)
            {
                logger.Warn("Failed to save feature {0}: {1}", null, layer.get_UpdateSourceErrorGeometryIndex(i),
                    layer.get_UpdateSourceErrorMsg(i));
            }
        }

        public void CreateLayer()
        {
            var view = _context.Container.GetSingleton<CreateLayerPresenter>();
            if (view.Run())
            {
                var fs = new FeatureSet(view.GeometryType, view.ZValueType);
                fs.Projection.CopyFrom(_context.Map.Projection);
                fs.SaveAs(view.Filename);
                fs.InteractiveEditing = true;
                _context.Layers.Add(fs);
            }
        }
    }
}