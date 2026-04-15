import React from 'react';
import { useLanguage } from '@/contexts/LanguageContext';
import type { GradingResult } from '@/types';
import { QualityGrade } from '@/types';

interface GradingResultDisplayProps {
  result: GradingResult;
  imageUrl?: string;
  onSave?: () => void;
}

const gradeColors: Record<QualityGrade, { bg: string; text: string; border: string }> = {
  [QualityGrade.A]: { bg: 'bg-green-100 dark:bg-green-900/30', text: 'text-green-800 dark:text-green-200', border: 'border-green-300 dark:border-green-700' },
  [QualityGrade.B]: { bg: 'bg-blue-100 dark:bg-blue-900/30', text: 'text-blue-800 dark:text-blue-200', border: 'border-blue-300 dark:border-blue-700' },
  [QualityGrade.C]: { bg: 'bg-yellow-100 dark:bg-yellow-900/30', text: 'text-yellow-800 dark:text-yellow-200', border: 'border-yellow-300 dark:border-yellow-700' },
  [QualityGrade.Reject]: { bg: 'bg-red-100 dark:bg-red-900/30', text: 'text-red-800 dark:text-red-200', border: 'border-red-300 dark:border-red-700' }
};

export const GradingResultDisplay: React.FC<GradingResultDisplayProps> = ({
  result,
  imageUrl,
  onSave
}) => {
  const { t } = useLanguage();
  const colors = gradeColors[result.grade] || gradeColors[QualityGrade.C]; // Fallback to C grade if undefined

  return (
    <div className="bg-white dark:bg-gray-800 rounded-lg shadow-md overflow-hidden">
      {/* Image Preview */}
      {imageUrl && (
        <div className="relative h-64 bg-gray-100 dark:bg-gray-900">
          <img
            src={imageUrl}
            alt="Graded produce"
            className="w-full h-full object-contain"
          />
        </div>
      )}

      <div className="p-6 space-y-6">
        {/* Grade Badge */}
        <div className="flex items-center justify-between">
          <div>
            <h3 className="text-lg font-semibold text-gray-900 dark:text-gray-100 mb-2">
              {t('grading.qualityGrade', 'Quality Grade')}
            </h3>
            <div className={`inline-flex items-center px-6 py-3 rounded-lg border-2 ${colors.bg} ${colors.text} ${colors.border}`}>
              <span className="text-3xl font-bold">{result.grade}</span>
            </div>
          </div>

          {/* Certified Price */}
          <div className="text-right">
            <p className="text-sm text-gray-600 dark:text-gray-400 mb-1">
              {t('grading.certifiedPrice', 'Certified Price')}
            </p>
            <p className="text-2xl font-bold text-green-600 dark:text-green-400">
              ₹{result.certifiedPrice.toFixed(2)}
            </p>
            <p className="text-xs text-gray-500 dark:text-gray-400">
              {t('grading.perQuintal', 'per 100kg (quintal)')}
            </p>
          </div>
        </div>

        {/* Confidence Score */}
        <div>
          <div className="flex items-center justify-between mb-2">
            <span className="text-sm font-medium text-gray-700 dark:text-gray-300">
              {t('grading.confidenceScore', 'Confidence Score')}
            </span>
            <span className="text-sm font-semibold text-gray-900 dark:text-gray-100">
              {(result.analysis.confidenceScore * 100).toFixed(1)}%
            </span>
          </div>
          <div className="w-full bg-gray-200 dark:bg-gray-700 rounded-full h-2">
            <div
              className="bg-green-600 h-2 rounded-full transition-all duration-300"
              style={{ width: `${result.analysis.confidenceScore * 100}%` }}
            />
          </div>
        </div>

        {/* Quality Indicators */}
        <div>
          <h4 className="text-sm font-semibold text-gray-900 dark:text-gray-100 mb-3">
            {t('grading.qualityIndicators', 'Quality Indicators')}
          </h4>
          <div className="space-y-2">
            {result.analysis.qualityIndicators.map((indicator, index) => (
              <div key={index} className="flex items-center justify-between">
                <span className="text-sm text-gray-700 dark:text-gray-300">
                  {indicator.name}
                </span>
                <div className="flex items-center space-x-2">
                  <span className="text-sm font-medium text-gray-900 dark:text-gray-100">
                    {indicator.value.toFixed(1)}
                  </span>
                  <span className={`text-xs px-2 py-1 rounded ${
                    indicator.status === 'good' ? 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-200' :
                    indicator.status === 'fair' ? 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900/30 dark:text-yellow-200' :
                    'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-200'
                  }`}>
                    {indicator.status}
                  </span>
                </div>
              </div>
            ))}
          </div>
        </div>

        {/* Image Quality */}
        <div>
          <h4 className="text-sm font-semibold text-gray-900 dark:text-gray-100 mb-3">
            {t('grading.imageQuality', 'Image Quality')}
          </h4>
          <div className="grid grid-cols-3 gap-4">
            <div className="text-center">
              <p className="text-xs text-gray-600 dark:text-gray-400 mb-1">
                {t('grading.resolution', 'Resolution')}
              </p>
              <p className="text-sm font-medium text-gray-900 dark:text-gray-100">
                {result.analysis.imageQuality.resolution}
              </p>
            </div>
            <div className="text-center">
              <p className="text-xs text-gray-600 dark:text-gray-400 mb-1">
                {t('grading.clarity', 'Clarity')}
              </p>
              <p className="text-sm font-medium text-gray-900 dark:text-gray-100">
                {(result.analysis.imageQuality.clarity * 100).toFixed(0)}%
              </p>
            </div>
            <div className="text-center">
              <p className="text-xs text-gray-600 dark:text-gray-400 mb-1">
                {t('grading.lighting', 'Lighting')}
              </p>
              <p className="text-sm font-medium text-gray-900 dark:text-gray-100">
                {(result.analysis.imageQuality.lighting * 100).toFixed(0)}%
              </p>
            </div>
          </div>
        </div>

        {/* Detected Objects */}
        {result.analysis.detectedObjects.length > 0 && (
          <div>
            <h4 className="text-sm font-semibold text-gray-900 dark:text-gray-100 mb-3">
              {t('grading.detectedObjects', 'Detected Objects')}
            </h4>
            <div className="flex flex-wrap gap-2">
              {result.analysis.detectedObjects.map((obj, index) => (
                <span
                  key={index}
                  className="inline-flex items-center px-3 py-1 rounded-full text-xs font-medium bg-gray-100 text-gray-800 dark:bg-gray-700 dark:text-gray-200"
                >
                  {obj.label} ({(obj.confidence * 100).toFixed(0)}%)
                </span>
              ))}
            </div>
          </div>
        )}

        {/* Timestamp */}
        <div className="pt-4 border-t border-gray-200 dark:border-gray-700">
          <p className="text-xs text-gray-500 dark:text-gray-400">
            {t('grading.gradedOn', 'Graded on')}: {new Date(result.timestamp).toLocaleString()}
          </p>
          <p className="text-xs text-gray-500 dark:text-gray-400">
            {t('grading.recordId', 'Record ID')}: {result.recordId}
          </p>
        </div>

        {/* Save Button */}
        {onSave && (
          <button
            onClick={onSave}
            className="w-full px-4 py-2 bg-green-600 text-white rounded-md hover:bg-green-700 transition-colors"
          >
            {t('grading.saveResult', 'Save Result')}
          </button>
        )}
      </div>
    </div>
  );
};
