import React from 'react';
import { useLanguage } from '@/contexts/LanguageContext';
import type { PlantingWindow, SeedRecommendation } from '@/services/plantingAdvisoryService';
import { ConfidenceScoreDisplay } from '@/components/soil/ConfidenceScoreDisplay';

interface RecommendationDisplayProps {
  plantingWindows: PlantingWindow[];
  seedRecommendations: SeedRecommendation[];
  weatherFetchedAt?: string;
  soilDataDate?: string;
  usedPlanId?: string;
  onSave?: () => void;
}

export const RecommendationDisplay: React.FC<RecommendationDisplayProps> = ({
  plantingWindows,
  seedRecommendations,
  weatherFetchedAt,
  soilDataDate,
  usedPlanId,
  onSave
}) => {
  const { currentLanguage } = useLanguage();
  const language = currentLanguage.code;

  const formatDate = (dateString: string) => {
    const date = new Date(dateString);
    return date.toLocaleDateString(language === 'hi' ? 'hi-IN' : 'en-IN', {
      year: 'numeric',
      month: 'long',
      day: 'numeric'
    });
  };

  return (
    <div className="space-y-6">
      {/* Data Sources Info */}
      {(weatherFetchedAt || soilDataDate || usedPlanId) && (
        <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
          <h3 className="text-sm font-medium text-blue-900 mb-2">
            {language === 'hi' ? 'डेटा स्रोत' : 'Data Sources'}
          </h3>
          <div className="text-sm text-blue-700 space-y-1">
            {usedPlanId && (
              <p className="font-medium">
                {language === 'hi' ? '📋 सहेजी गई योजना: ' : '📋 Saved Plan: '}
                #{usedPlanId.slice(0, 8)}
              </p>
            )}
            {weatherFetchedAt && (
              <p>
                {language === 'hi' ? '🌤️ मौसम डेटा: ' : '🌤️ Weather Data: '}
                {formatDate(weatherFetchedAt)}
              </p>
            )}
            {soilDataDate && (
              <p>
                {language === 'hi' ? '🌱 मृदा डेटा: ' : '🌱 Soil Data: '}
                {formatDate(soilDataDate)}
              </p>
            )}
          </div>
        </div>
      )}

      {/* Planting Windows */}
      <div>
        <h3 className="text-xl font-semibold text-gray-900 mb-4">
          {language === 'hi' ? 'इष्टतम रोपण खिड़कियां' : 'Optimal Planting Windows'}
        </h3>
        <div className="space-y-4">
          {plantingWindows.map((window, index) => (
            <div
              key={index}
              className="bg-white border border-gray-200 rounded-lg p-5 shadow-sm hover:shadow-md transition-shadow"
            >
              <div className="flex items-start justify-between mb-3">
                <div className="flex-1">
                  <div className="flex items-center gap-2 mb-2">
                    <span className="text-lg font-semibold text-gray-900">
                      {language === 'hi' ? `खिड़की ${index + 1}` : `Window ${index + 1}`}
                    </span>
                    {index === 0 && (
                      <span className="px-2 py-1 bg-green-100 text-green-800 text-xs font-medium rounded">
                        {language === 'hi' ? 'सर्वोत्तम' : 'Best'}
                      </span>
                    )}
                  </div>
                  <p className="text-sm text-gray-600">
                    {formatDate(window.startDate)} - {formatDate(window.endDate)}
                  </p>
                </div>
                <ConfidenceScoreDisplay score={window.confidenceScore} />
              </div>

              <div className="mb-3">
                <p className="text-sm font-medium text-gray-700 mb-1">
                  {language === 'hi' ? 'तर्क:' : 'Rationale:'}
                </p>
                <p className="text-sm text-gray-600">{window.rationale}</p>
              </div>

              {window.riskFactors && window.riskFactors.length > 0 && (
                <div>
                  <p className="text-sm font-medium text-gray-700 mb-2">
                    {language === 'hi' ? '⚠️ जोखिम कारक:' : '⚠️ Risk Factors:'}
                  </p>
                  <ul className="list-disc list-inside space-y-1">
                    {window.riskFactors.map((risk, idx) => (
                      <li key={idx} className="text-sm text-orange-700">
                        {risk}
                      </li>
                    ))}
                  </ul>
                </div>
              )}
            </div>
          ))}
        </div>
      </div>

      {/* Seed Recommendations */}
      {seedRecommendations && seedRecommendations.length > 0 && (
        <div>
          <h3 className="text-xl font-semibold text-gray-900 mb-4">
            {language === 'hi' ? 'बीज किस्म की सिफारिशें' : 'Seed Variety Recommendations'}
          </h3>
          <div className="grid gap-4 md:grid-cols-2">
            {seedRecommendations.map((seed, index) => (
              <div
                key={index}
                className="bg-white border border-gray-200 rounded-lg p-5 shadow-sm hover:shadow-md transition-shadow"
              >
                <h4 className="text-lg font-semibold text-gray-900 mb-1">
                  {seed.varietyName}
                </h4>
                <p className="text-sm text-gray-600 mb-3">{seed.seedCompany}</p>

                <div className="space-y-2 mb-3">
                  <div className="flex justify-between text-sm">
                    <span className="text-gray-600">
                      {language === 'hi' ? 'परिपक्वता अवधि:' : 'Maturity Period:'}
                    </span>
                    <span className="font-medium text-gray-900">
                      {seed.maturityDays} {language === 'hi' ? 'दिन' : 'days'}
                    </span>
                  </div>
                  <div className="flex justify-between text-sm">
                    <span className="text-gray-600">
                      {language === 'hi' ? 'उपज क्षमता:' : 'Yield Potential:'}
                    </span>
                    <span className="font-medium text-gray-900">
                      {seed.yieldPotential} {language === 'hi' ? 'टन/हेक्टेयर' : 'tons/hectare'}
                    </span>
                  </div>
                </div>

                <div className="mb-3">
                  <p className="text-sm font-medium text-gray-700 mb-1">
                    {language === 'hi' ? 'उपयुक्तता:' : 'Suitability:'}
                  </p>
                  <p className="text-sm text-gray-600">{seed.suitabilityReason}</p>
                </div>

                {seed.keyCharacteristics && seed.keyCharacteristics.length > 0 && (
                  <div>
                    <p className="text-sm font-medium text-gray-700 mb-2">
                      {language === 'hi' ? 'मुख्य विशेषताएं:' : 'Key Characteristics:'}
                    </p>
                    <ul className="list-disc list-inside space-y-1">
                      {seed.keyCharacteristics.map((char, idx) => (
                        <li key={idx} className="text-sm text-gray-600">
                          {char}
                        </li>
                      ))}
                    </ul>
                  </div>
                )}
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Save Button */}
      {onSave && (
        <div className="flex justify-center pt-4">
          <button
            onClick={onSave}
            className="px-6 py-3 bg-green-600 text-white rounded-lg font-medium hover:bg-green-700 transition-colors"
          >
            {language === 'hi' ? '💾 सिफारिश सहेजें' : '💾 Save Recommendation'}
          </button>
        </div>
      )}
    </div>
  );
};
