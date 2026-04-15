import React, { useState } from 'react';
import { useLanguage } from '@/contexts/LanguageContext';
import type { RegenerativePlan, PlanRecommendation, PlanTimeline } from '@/services/soilAnalysisService';

interface RegenerativePlanDisplayProps {
  plan: RegenerativePlan;
  onSave?: () => void;
}

export const RegenerativePlanDisplay: React.FC<RegenerativePlanDisplayProps> = ({
  plan,
  onSave
}) => {
  const { t } = useLanguage();
  const [activeTab, setActiveTab] = useState<'recommendations' | 'timeline'>('recommendations');

  const getPriorityColor = (priority: 'high' | 'medium' | 'low'): string => {
    switch (priority) {
      case 'high': return 'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-300';
      case 'medium': return 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900/30 dark:text-yellow-300';
      case 'low': return 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-300';
    }
  };

  const getPriorityText = (priority: 'high' | 'medium' | 'low'): string => {
    switch (priority) {
      case 'high': return t('soilAnalysis.highPriority', 'High Priority');
      case 'medium': return t('soilAnalysis.mediumPriority', 'Medium Priority');
      case 'low': return t('soilAnalysis.lowPriority', 'Low Priority');
    }
  };

  const RecommendationCard: React.FC<{ recommendation: PlanRecommendation }> = ({ recommendation }) => (
    <div className="bg-white dark:bg-gray-800 rounded-lg shadow-md p-6 border-l-4 border-green-500">
      <div className="flex items-start justify-between mb-3">
        <div className="flex-1">
          <h4 className="text-lg font-semibold text-gray-900 dark:text-gray-100 mb-1">
            {recommendation.title}
          </h4>
          <span className="text-xs text-gray-500 dark:text-gray-400">
            {recommendation.category}
          </span>
        </div>
        <span className={`px-3 py-1 rounded-full text-xs font-medium ${getPriorityColor(recommendation.priority)}`}>
          {getPriorityText(recommendation.priority)}
        </span>
      </div>

      <p className="text-gray-700 dark:text-gray-300 mb-4">
        {recommendation.description}
      </p>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-4">
        <div className="bg-gray-50 dark:bg-gray-700/50 rounded-lg p-3">
          <p className="text-xs text-gray-500 dark:text-gray-400 mb-1">
            {t('soilAnalysis.estimatedCost', 'Estimated Cost')}
          </p>
          <p className="text-lg font-semibold text-gray-900 dark:text-gray-100">
            ₹{recommendation.estimatedCost.toLocaleString()}
          </p>
        </div>
        <div className="bg-gray-50 dark:bg-gray-700/50 rounded-lg p-3">
          <p className="text-xs text-gray-500 dark:text-gray-400 mb-1">
            {t('soilAnalysis.expectedBenefit', 'Expected Benefit')}
          </p>
          <p className="text-sm font-medium text-green-600 dark:text-green-400">
            {recommendation.expectedBenefit}
          </p>
        </div>
      </div>

      {recommendation.implementationSteps && recommendation.implementationSteps.length > 0 && (
        <div>
          <h5 className="text-sm font-semibold text-gray-900 dark:text-gray-100 mb-2">
            {t('soilAnalysis.implementationSteps', 'Implementation Steps')}
          </h5>
          <ol className="list-decimal list-inside space-y-1">
            {recommendation.implementationSteps.map((step, index) => (
              <li key={index} className="text-sm text-gray-700 dark:text-gray-300">
                {step}
              </li>
            ))}
          </ol>
        </div>
      )}
    </div>
  );

  const TimelineCard: React.FC<{ timeline: PlanTimeline }> = ({ timeline }) => (
    <div className="bg-white dark:bg-gray-800 rounded-lg shadow-md p-6">
      <div className="flex items-center mb-4">
        <div className="w-12 h-12 bg-green-100 dark:bg-green-900/30 rounded-full flex items-center justify-center mr-4">
          <span className="text-lg font-bold text-green-600 dark:text-green-400">
            {timeline.month}
          </span>
        </div>
        <h4 className="text-lg font-semibold text-gray-900 dark:text-gray-100">
          {timeline.monthName}
        </h4>
      </div>

      <div className="space-y-4">
        {timeline.practices && timeline.practices.length > 0 && (
          <div>
            <h5 className="text-sm font-semibold text-gray-700 dark:text-gray-300 mb-2">
              {t('soilAnalysis.practices', 'Practices')}
            </h5>
            <ul className="space-y-1">
              {timeline.practices.map((practice, index) => (
                <li key={index} className="flex items-start">
                  <svg
                    className="w-5 h-5 text-green-500 mt-0.5 mr-2 flex-shrink-0"
                    fill="currentColor"
                    viewBox="0 0 20 20"
                  >
                    <path
                      fillRule="evenodd"
                      d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z"
                      clipRule="evenodd"
                    />
                  </svg>
                  <span className="text-sm text-gray-700 dark:text-gray-300">{practice}</span>
                </li>
              ))}
            </ul>
          </div>
        )}

        {timeline.rationale && (
          <div>
            <h5 className="text-sm font-semibold text-gray-700 dark:text-gray-300 mb-2">
              {t('soilAnalysis.rationale', 'Rationale')}
            </h5>
            <p className="text-sm text-gray-600 dark:text-gray-400">{timeline.rationale}</p>
          </div>
        )}

        {timeline.expectedOutcomes && timeline.expectedOutcomes.length > 0 && (
          <div>
            <h5 className="text-sm font-semibold text-gray-700 dark:text-gray-300 mb-2">
              {t('soilAnalysis.expectedOutcomes', 'Expected Outcomes')}
            </h5>
            <ul className="space-y-1">
              {timeline.expectedOutcomes.map((outcome, index) => (
                <li key={index} className="flex items-start">
                  <svg
                    className="w-5 h-5 text-blue-500 mt-0.5 mr-2 flex-shrink-0"
                    fill="currentColor"
                    viewBox="0 0 20 20"
                  >
                    <path
                      fillRule="evenodd"
                      d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7-4a1 1 0 11-2 0 1 1 0 012 0zM9 9a1 1 0 000 2v3a1 1 0 001 1h1a1 1 0 100-2v-3a1 1 0 00-1-1H9z"
                      clipRule="evenodd"
                    />
                  </svg>
                  <span className="text-sm text-gray-700 dark:text-gray-300">{outcome}</span>
                </li>
              ))}
            </ul>
          </div>
        )}
      </div>
    </div>
  );

  return (
    <div className="space-y-6">
      {/* Plan Header */}
      <div className="bg-gradient-to-r from-green-600 to-green-700 rounded-lg shadow-lg p-6 text-white">
        <h2 className="text-2xl font-bold mb-2">
          {t('soilAnalysis.regenerativePlan', 'Regenerative Farming Plan')}
        </h2>
        <p className="text-green-100 mb-4">
          {t('soilAnalysis.planGenerated', 'Generated on')}: {new Date(plan.createdAt).toLocaleDateString()}
        </p>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <div className="bg-white/10 rounded-lg p-4">
            <p className="text-sm text-green-100 mb-1">
              {t('soilAnalysis.carbonSequestration', 'Carbon Sequestration Potential')}
            </p>
            <p className="text-2xl font-bold">
              {plan.carbonEstimate?.totalCarbonTonnesPerYear?.toFixed(2) || '0.00'} t CO₂/year
            </p>
          </div>
          <div className="bg-white/10 rounded-lg p-4">
            <p className="text-sm text-green-100 mb-1">
              {t('soilAnalysis.monthlyAverage', 'Monthly Average')}
            </p>
            <p className="text-2xl font-bold">
              {plan.carbonEstimate?.monthlyAverageTonnes?.toFixed(3) || '0.000'} t CO₂
            </p>
          </div>
          <div className="bg-white/10 rounded-lg p-4">
            <p className="text-sm text-green-100 mb-1">
              {t('soilAnalysis.validUntil', 'Valid Until')}
            </p>
            <p className="text-2xl font-bold">
              {plan.validUntil ? new Date(plan.validUntil).toLocaleDateString() : 'N/A'}
            </p>
          </div>
        </div>

        {onSave && (
          <button
            onClick={onSave}
            className="mt-4 px-6 py-2 bg-white text-green-700 rounded-md hover:bg-green-50 font-medium transition-colors"
          >
            {t('soilAnalysis.savePlan', 'Save Plan')}
          </button>
        )}
      </div>

      {/* Tabs */}
      <div className="border-b border-gray-200 dark:border-gray-700">
        <nav className="flex space-x-8">
          <button
            onClick={() => setActiveTab('recommendations')}
            className={`py-4 px-1 border-b-2 font-medium text-sm transition-colors ${
              activeTab === 'recommendations'
                ? 'border-green-500 text-green-600 dark:text-green-400'
                : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300 dark:text-gray-400 dark:hover:text-gray-300'
            }`}
          >
            {t('soilAnalysis.recommendations', 'Recommendations')}
          </button>
          <button
            onClick={() => setActiveTab('timeline')}
            className={`py-4 px-1 border-b-2 font-medium text-sm transition-colors ${
              activeTab === 'timeline'
                ? 'border-green-500 text-green-600 dark:text-green-400'
                : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300 dark:text-gray-400 dark:hover:text-gray-300'
            }`}
          >
            {t('soilAnalysis.timeline', '12-Month Timeline')}
          </button>
        </nav>
      </div>

      {/* Tab Content */}
      <div>
        {activeTab === 'recommendations' && (
          <div className="space-y-4">
            {plan.recommendations && plan.recommendations.length > 0 ? (
              plan.recommendations.map((recommendation, index) => (
                <RecommendationCard key={index} recommendation={recommendation} />
              ))
            ) : (
              <p className="text-gray-500 dark:text-gray-400 text-center py-8">
                {t('soilAnalysis.noRecommendations', 'No recommendations available')}
              </p>
            )}
          </div>
        )}

        {activeTab === 'timeline' && (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {plan.monthlyActions.map((timeline, index) => (
              <TimelineCard key={index} timeline={timeline} />
            ))}
          </div>
        )}
      </div>
    </div>
  );
};
