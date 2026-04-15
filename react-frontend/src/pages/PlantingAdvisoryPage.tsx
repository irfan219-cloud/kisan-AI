import React, { useState } from 'react';
import { useLanguage } from '@/contexts/LanguageContext';
import { useNotifications } from '@/hooks/useNotifications';
import { plantingAdvisoryService, type PlantingRecommendationResponse } from '@/services/plantingAdvisoryService';
import { PlantingAdvisoryForm } from '@/components/planting/PlantingAdvisoryForm';
import { RecommendationDisplay } from '@/components/planting/RecommendationDisplay';
import { SavedRecommendations } from '@/components/planting/SavedRecommendations';
import { LoadingSpinner } from '@/components/common/LoadingSpinner';
import { ErrorDisplay } from '@/components/error/ErrorDisplay';

type ViewMode = 'form' | 'results' | 'saved';

export const PlantingAdvisoryPage: React.FC = () => {
  const { currentLanguage } = useLanguage();
  const language = currentLanguage.code;
  const { showNotification } = useNotifications();
  const [viewMode, setViewMode] = useState<ViewMode>('form');
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [recommendation, setRecommendation] = useState<PlantingRecommendationResponse | null>(null);
  const [currentCropType, setCurrentCropType] = useState<string>('');
  const [currentLocation, setCurrentLocation] = useState<string>('');

  const handleGetRecommendations = async (
    cropType: string,
    location: string,
    forecastDays: number,
    planId?: string
  ) => {
    setIsLoading(true);
    setError(null);
    setCurrentCropType(cropType);
    setCurrentLocation(location);

    try {
      const response = await plantingAdvisoryService.getPlantingRecommendation({
        cropType,
        location,
        forecastDays,
        planId
      });

      setRecommendation(response);

      if (response.hasRecommendations) {
        setViewMode('results');
        const successMessage = planId 
          ? (language === 'hi' 
              ? `सहेजी गई योजना से सिफारिशें प्राप्त की गईं (योजना ID: ${response.usedPlanId?.slice(0, 8)})` 
              : `Recommendations generated using saved plan (Plan ID: ${response.usedPlanId?.slice(0, 8)})`)
          : response.message;
        
        showNotification({
          type: 'success',
          title: language === 'hi' ? 'सफलता' : 'Success',
          message: successMessage
        });
      } else {
        // No recommendations found
        showNotification({
          type: 'warning',
          title: language === 'hi' ? 'कोई सिफारिश नहीं' : 'No Recommendations',
          message: response.message
        });
      }
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to get recommendations';
      setError(errorMessage);
      
      // Check for specific error codes
      if (errorMessage.includes('SOIL_DATA_NOT_FOUND') || errorMessage.includes('soil data')) {
        showNotification({
          type: 'error',
          title: language === 'hi' ? 'मृदा डेटा आवश्यक' : 'Soil Data Required',
          message: language === 'hi' 
            ? 'कृपया पहले अपना मृदा स्वास्थ्य कार्ड अपलोड करें या एक सहेजी गई योजना चुनें' 
            : 'Please upload your Soil Health Card first or select a saved plan'
        });
      } else {
        showNotification({
          type: 'error',
          title: language === 'hi' ? 'त्रुटि' : 'Error',
          message: errorMessage
        });
      }
    } finally {
      setIsLoading(false);
    }
  };

  const handleSaveRecommendation = async () => {
    if (recommendation && currentCropType && currentLocation) {
      try {
        await plantingAdvisoryService.saveRecommendation(
          recommendation,
          currentCropType,
          currentLocation
        );
        showNotification({
          type: 'success',
          title: language === 'hi' ? 'सहेजा गया' : 'Saved',
          message: language === 'hi' 
            ? 'सिफारिश सफलतापूर्वक सहेजी गई' 
            : 'Recommendation saved successfully'
        });
      } catch (err) {
        showNotification({
          type: 'error',
          title: language === 'hi' ? 'त्रुटि' : 'Error',
          message: language === 'hi' 
            ? 'सिफारिश सहेजने में विफल' 
            : 'Failed to save recommendation'
        });
      }
    }
  };

  const handleRetry = () => {
    setError(null);
    setViewMode('form');
  };

  const handleNewRecommendation = () => {
    setRecommendation(null);
    setError(null);
    setViewMode('form');
  };

  return (
    <div className="min-h-screen bg-gray-50 py-8 px-4 sm:px-6 lg:px-8">
      <div className="max-w-4xl mx-auto">
        {/* Header */}
        <div className="mb-8">
          <h1 className="text-3xl font-bold text-gray-900 mb-2">
            {language === 'hi' ? '🌱 रोपण सलाहकार' : '🌱 Planting Advisory'}
          </h1>
          <p className="text-gray-600">
            {language === 'hi' 
              ? 'मौसम और मृदा डेटा के आधार पर रोपण सिफारिशें प्राप्त करें' 
              : 'Get planting recommendations based on weather and soil data'}
          </p>
        </div>

        {/* View Mode Tabs */}
        <div className="mb-6 flex gap-2 border-b border-gray-200">
          <button
            onClick={() => setViewMode('form')}
            className={`px-4 py-2 font-medium transition-colors ${
              viewMode === 'form'
                ? 'text-green-600 border-b-2 border-green-600'
                : 'text-gray-600 hover:text-gray-900'
            }`}
          >
            {language === 'hi' ? 'नई सिफारिश' : 'New Recommendation'}
          </button>
          <button
            onClick={() => setViewMode('saved')}
            className={`px-4 py-2 font-medium transition-colors ${
              viewMode === 'saved'
                ? 'text-green-600 border-b-2 border-green-600'
                : 'text-gray-600 hover:text-gray-900'
            }`}
          >
            {language === 'hi' ? 'सहेजी गई सिफारिशें' : 'Saved Recommendations'}
          </button>
        </div>

        {/* Content */}
        <div className="bg-white rounded-lg shadow-md p-6">
          {viewMode === 'form' && (
            <>
              {/* Prerequisite Info */}
              <div className="mb-6 bg-blue-50 border border-blue-200 rounded-lg p-4">
                <h3 className="text-sm font-medium text-blue-900 mb-2">
                  ℹ️ {language === 'hi' ? 'आवश्यक शर्तें' : 'Prerequisites'}
                </h3>
                <p className="text-sm text-blue-700">
                  {language === 'hi' 
                    ? 'रोपण सिफारिशें प्राप्त करने के लिए, आप या तो एक सहेजी गई मृदा योजना चुन सकते हैं या नया मृदा स्वास्थ्य कार्ड अपलोड कर सकते हैं। सहेजी गई योजनाएं ड्रॉपडाउन में उपलब्ध हैं।' 
                    : 'To get planting recommendations, you can either select a saved soil plan or upload a new Soil Health Card. Saved plans are available in the dropdown above.'}
                </p>
              </div>

              {error && (
                <div className="mb-6">
                  <ErrorDisplay
                    error={{ code: 'ERROR', message: error, userFriendlyMessage: error }}
                    onRetry={handleRetry}
                  />
                </div>
              )}

              <PlantingAdvisoryForm
                onSubmit={handleGetRecommendations}
                isLoading={isLoading}
              />

              {isLoading && (
                <div className="mt-6 flex justify-center">
                  <LoadingSpinner />
                </div>
              )}
            </>
          )}

          {viewMode === 'results' && recommendation && (
            <>
              <div className="mb-6 flex justify-between items-center">
                <button
                  onClick={handleNewRecommendation}
                  className="text-green-600 hover:text-green-700 font-medium flex items-center gap-2"
                >
                  ← {language === 'hi' ? 'नई सिफारिश' : 'New Recommendation'}
                </button>
              </div>

              {recommendation.hasRecommendations ? (
                <RecommendationDisplay
                  plantingWindows={recommendation.plantingWindows}
                  seedRecommendations={recommendation.seedRecommendations}
                  weatherFetchedAt={recommendation.weatherFetchedAt}
                  soilDataDate={recommendation.soilDataDate}
                  usedPlanId={recommendation.usedPlanId}
                  onSave={handleSaveRecommendation}
                />
              ) : (
                <div className="text-center py-12">
                  <div className="text-6xl mb-4">🌾</div>
                  <h3 className="text-lg font-medium text-gray-900 mb-2">
                    {language === 'hi' ? 'कोई उपयुक्त रोपण खिड़की नहीं' : 'No Suitable Planting Windows'}
                  </h3>
                  <p className="text-gray-600 mb-4">
                    {recommendation.message}
                  </p>
                  <button
                    onClick={handleNewRecommendation}
                    className="px-6 py-2 bg-green-600 text-white rounded-lg hover:bg-green-700 transition-colors"
                  >
                    {language === 'hi' ? 'फिर से प्रयास करें' : 'Try Again'}
                  </button>
                </div>
              )}
            </>
          )}

          {viewMode === 'saved' && <SavedRecommendations />}
        </div>
      </div>
    </div>
  );
};

export default PlantingAdvisoryPage;
