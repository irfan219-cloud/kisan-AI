import React, { useState, useEffect } from 'react';
import { useLanguage } from '@/contexts/LanguageContext';
import { useAuth } from '@/contexts/AuthContext';
import { FileUploadZone } from '@/components/upload/FileUploadZone';
import { UploadProgress } from '@/components/upload/UploadProgress';
import { SoilDataForm } from '@/components/soil/SoilDataForm';
import { SoilDataDisplay } from '@/components/soil/SoilDataDisplay';
import { SoilHistoryChart } from '@/components/soil/SoilHistoryChart';
import { RegenerativePlanDisplay } from '@/components/soil/RegenerativePlanDisplay';
import { ConfidenceScoreDisplay } from '@/components/soil/ConfidenceScoreDisplay';
import { SavedPlansView } from '@/components/soil/SavedPlansView';
import { soilAnalysisService, type RegenerativePlan, type FarmProfile } from '@/services/soilAnalysisService';
import type { SoilHealthCardResponse, SoilHealthData } from '@/types';
import { LoadingSpinner } from '@/components/common/LoadingSpinner';
import { validateSoilData, normalizeSoilData, calculateConfidenceScore } from '@/utils/soilDataValidation';

type ViewMode = 'upload' | 'review' | 'history' | 'plan' | 'saved';

export const SoilAnalysisPage: React.FC = () => {
  const { t } = useLanguage();
  const { user } = useAuth();
  
  const [viewMode, setViewMode] = useState<ViewMode>('upload');
  const [uploadProgress, setUploadProgress] = useState<number>(0);
  const [isUploading, setIsUploading] = useState(false);
  const [isProcessing, setIsProcessing] = useState(false);
  const [uploadError, setUploadError] = useState<string | null>(null);
  
  const [soilCardResponse, setSoilCardResponse] = useState<SoilHealthCardResponse | null>(null);
  const [soilHistory, setSoilHistory] = useState<SoilHealthData[]>([]);
  const [regenerativePlan, setRegenerativePlan] = useState<RegenerativePlan | null>(null);
  const [isLoadingHistory, setIsLoadingHistory] = useState(false);
  const [isGeneratingPlan, setIsGeneratingPlan] = useState(false);

  // Load soil history on mount
  useEffect(() => {
    loadSoilHistory();
  }, []);

  const loadSoilHistory = async () => {
    setIsLoadingHistory(true);
    try {
      const history = await soilAnalysisService.getSoilHistory();
      setSoilHistory(history);
    } catch (error) {
      console.error('Failed to load soil history:', error);
    } finally {
      setIsLoadingHistory(false);
    }
  };

  const handleFileUpload = async (files: File[]) => {
    if (files.length === 0) return;

    const file = files[0];
    setIsUploading(true);
    setUploadError(null);
    setUploadProgress(0);

    try {
      const response = await soilAnalysisService.uploadSoilHealthCard(
        file,
        (progress) => setUploadProgress(progress)
      );

      setSoilCardResponse(response);
      
      if (response.isValid) {
        setViewMode('review');
      } else {
        // Show form for manual correction
        setViewMode('review');
      }

      // Reload history
      await loadSoilHistory();
    } catch (error) {
      setUploadError(error instanceof Error ? error.message : 'Upload failed');
    } finally {
      setIsUploading(false);
      setUploadProgress(0);
    }
  };

  const handleManualDataSubmit = async (data: SoilHealthData) => {
    setIsProcessing(true);
    try {
      // Normalize the data
      const normalizedData = normalizeSoilData(data) as SoilHealthData;
      
      // Validate the data
      const validationErrors = validateSoilData(normalizedData);
      
      if (validationErrors.length > 0) {
        // Update with validation errors
        setSoilCardResponse({
          soilData: normalizedData,
          isValid: false,
          validationErrors,
          message: t('soilAnalysis.validationFailed', 'Please correct the validation errors'),
          requiresManualVerification: true
        });
        return;
      }

      // Update the soil card response with corrected data
      setSoilCardResponse({
        soilData: normalizedData,
        isValid: true,
        validationErrors: [],
        message: t('soilAnalysis.dataUpdated', 'Soil data updated successfully'),
        requiresManualVerification: false
      });

      // Reload history
      await loadSoilHistory();
    } catch (error) {
      console.error('Failed to save soil data:', error);
    } finally {
      setIsProcessing(false);
    }
  };

  const handleGeneratePlan = async () => {
    if (!soilCardResponse?.soilData || !user) return;

    setIsGeneratingPlan(true);
    try {
      // Create farm profile (in a real app, this would come from user profile)
      const farmProfile: FarmProfile = {
        farmerId: user.id,
        farmName: user.name || 'My Farm',
        location: {
          state: 'Maharashtra',
          district: 'Pune',
          block: 'Haveli',
          village: 'Sample Village'
        },
        farmSize: 5,
        primaryCrops: ['Wheat', 'Rice'],
        soilType: soilCardResponse.soilData.soilTexture
      };

      const plan = await soilAnalysisService.generateRegenerativePlan({
        soilData: soilCardResponse.soilData,
        farmProfile
      });

      setRegenerativePlan(plan);
      setViewMode('plan');
    } catch (error) {
      console.error('Failed to generate plan:', error);
      alert(error instanceof Error ? error.message : 'Failed to generate plan');
    } finally {
      setIsGeneratingPlan(false);
    }
  };

  const handleSavePlan = async () => {
    if (!regenerativePlan) return;
    
    try {
      await soilAnalysisService.savePlan(regenerativePlan);
      alert(t('soilAnalysis.planSaved', 'Plan saved successfully!'));
    } catch (error) {
      alert(t('soilAnalysis.saveFailed', 'Failed to save plan. Saved locally as fallback.'));
    }
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="bg-white dark:bg-gray-800 rounded-lg shadow-md p-6">
        <h1 className="text-3xl font-bold text-gray-900 dark:text-gray-100 mb-2">
          {t('nav.soilAnalysis', 'Soil Analysis')}
        </h1>
        <p className="text-gray-600 dark:text-gray-300">
          {t('soilAnalysis.description', 'Upload your Soil Health Card to get digital analysis and regenerative farming recommendations')}
        </p>
      </div>

      {/* Navigation Tabs */}
      <div className="bg-white dark:bg-gray-800 rounded-lg shadow-md">
        <nav className="flex border-b border-gray-200 dark:border-gray-700">
          <button
            onClick={() => setViewMode('upload')}
            className={`px-6 py-4 text-sm font-medium border-b-2 transition-colors ${
              viewMode === 'upload'
                ? 'border-green-500 text-green-600 dark:text-green-400'
                : 'border-transparent text-gray-500 hover:text-gray-700 dark:text-gray-400'
            }`}
          >
            {t('soilAnalysis.uploadCard', 'Upload Card')}
          </button>
          <button
            onClick={() => setViewMode('review')}
            disabled={!soilCardResponse}
            className={`px-6 py-4 text-sm font-medium border-b-2 transition-colors ${
              viewMode === 'review'
                ? 'border-green-500 text-green-600 dark:text-green-400'
                : 'border-transparent text-gray-500 hover:text-gray-700 dark:text-gray-400'
            } disabled:opacity-50 disabled:cursor-not-allowed`}
          >
            {t('soilAnalysis.reviewData', 'Review Data')}
          </button>
          <button
            onClick={() => setViewMode('history')}
            className={`px-6 py-4 text-sm font-medium border-b-2 transition-colors ${
              viewMode === 'history'
                ? 'border-green-500 text-green-600 dark:text-green-400'
                : 'border-transparent text-gray-500 hover:text-gray-700 dark:text-gray-400'
            }`}
          >
            {t('soilAnalysis.history', 'History')}
          </button>
          <button
            onClick={() => setViewMode('plan')}
            disabled={!regenerativePlan}
            className={`px-6 py-4 text-sm font-medium border-b-2 transition-colors ${
              viewMode === 'plan'
                ? 'border-green-500 text-green-600 dark:text-green-400'
                : 'border-transparent text-gray-500 hover:text-gray-700 dark:text-gray-400'
            } disabled:opacity-50 disabled:cursor-not-allowed`}
          >
            {t('soilAnalysis.regenerativePlan', 'Regenerative Plan')}
          </button>
          <button
            onClick={() => setViewMode('saved')}
            className={`px-6 py-4 text-sm font-medium border-b-2 transition-colors ${
              viewMode === 'saved'
                ? 'border-green-500 text-green-600 dark:text-green-400'
                : 'border-transparent text-gray-500 hover:text-gray-700 dark:text-gray-400'
            }`}
          >
            {t('soilAnalysis.savedPlans', 'Saved Plans')}
          </button>
        </nav>
      </div>

      {/* Content Area */}
      <div>
        {/* Upload View */}
        {viewMode === 'upload' && (
          <div className="space-y-6">
            <div className="bg-white dark:bg-gray-800 rounded-lg shadow-md p-6">
              <h2 className="text-xl font-semibold text-gray-900 dark:text-gray-100 mb-4">
                {t('soilAnalysis.uploadSoilHealthCard', 'Upload Soil Health Card')}
              </h2>
              
              {!isUploading && !uploadError && (
                <FileUploadZone
                  accept={['image/jpeg', 'image/png', 'application/pdf', 'text/plain']}
                  maxSize={10 * 1024 * 1024}
                  maxFiles={1}
                  onUpload={handleFileUpload}
                  onProgress={setUploadProgress}
                  onError={setUploadError}
                />
              )}

              {isUploading && (
                <UploadProgress
                  progress={uploadProgress}
                  status="uploading"
                />
              )}

              {uploadError && (
                <div className="bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg p-4">
                  <p className="text-red-800 dark:text-red-200">{uploadError}</p>
                  <button
                    onClick={() => {
                      setUploadError(null);
                      setUploadProgress(0);
                    }}
                    className="mt-2 text-sm text-red-600 dark:text-red-400 hover:underline"
                  >
                    {t('common.tryAgain', 'Try Again')}
                  </button>
                </div>
              )}
            </div>

            <div className="bg-blue-50 dark:bg-blue-900/20 rounded-lg p-6">
              <h3 className="text-lg font-semibold text-blue-900 dark:text-blue-100 mb-2">
                {t('soilAnalysis.tips', 'Tips for Best Results')}
              </h3>
              <ul className="space-y-2 text-sm text-blue-800 dark:text-blue-200">
                <li className="flex items-start">
                  <svg className="w-5 h-5 mr-2 mt-0.5 flex-shrink-0" fill="currentColor" viewBox="0 0 20 20">
                    <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clipRule="evenodd" />
                  </svg>
                  {t('soilAnalysis.tip1', 'Take a clear photo in good lighting')}
                </li>
                <li className="flex items-start">
                  <svg className="w-5 h-5 mr-2 mt-0.5 flex-shrink-0" fill="currentColor" viewBox="0 0 20 20">
                    <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clipRule="evenodd" />
                  </svg>
                  {t('soilAnalysis.tip2', 'Ensure all text is visible and readable')}
                </li>
                <li className="flex items-start">
                  <svg className="w-5 h-5 mr-2 mt-0.5 flex-shrink-0" fill="currentColor" viewBox="0 0 20 20">
                    <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clipRule="evenodd" />
                  </svg>
                  {t('soilAnalysis.tip3', 'Supported formats: JPEG, PNG, PDF (max 10MB)')}
                </li>
              </ul>
            </div>
          </div>
        )}

        {/* Review View */}
        {viewMode === 'review' && soilCardResponse && (
          <div className="space-y-6">
            {/* Confidence Score Display */}
            {soilCardResponse.soilData && (
              <ConfidenceScoreDisplay
                score={calculateConfidenceScore(
                  soilCardResponse.soilData,
                  soilCardResponse.validationErrors
                )}
                showDetails={true}
              />
            )}

            {soilCardResponse.requiresManualVerification && (
              <div className="bg-yellow-50 dark:bg-yellow-900/20 border border-yellow-200 dark:border-yellow-800 rounded-lg p-4">
                <p className="text-yellow-800 dark:text-yellow-200 mb-2">
                  {soilCardResponse.message}
                </p>
                <p className="text-sm text-yellow-700 dark:text-yellow-300">
                  {t('soilAnalysis.pleaseReview', 'Please review and correct the data below')}
                </p>
              </div>
            )}

            {soilCardResponse.soilData && !soilCardResponse.requiresManualVerification && (
              <>
                <SoilDataDisplay soilData={soilCardResponse.soilData} />
                
                <div className="flex justify-end space-x-4">
                  <button
                    onClick={() => setViewMode('upload')}
                    className="px-6 py-2 border border-gray-300 rounded-md text-gray-700 hover:bg-gray-50"
                  >
                    {t('soilAnalysis.uploadAnother', 'Upload Another')}
                  </button>
                  <button
                    onClick={handleGeneratePlan}
                    disabled={isGeneratingPlan}
                    className="px-6 py-2 bg-green-600 text-white rounded-md hover:bg-green-700 disabled:opacity-50 disabled:cursor-not-allowed"
                  >
                    {isGeneratingPlan ? (
                      <span className="flex items-center">
                        <span className="mr-2">
                          <LoadingSpinner size="sm" />
                        </span>
                        {t('soilAnalysis.generating', 'Generating...')}
                      </span>
                    ) : (
                      t('soilAnalysis.generatePlan', 'Generate Regenerative Plan')
                    )}
                  </button>
                </div>
              </>
            )}

            {soilCardResponse.requiresManualVerification && soilCardResponse.soilData && (
              <SoilDataForm
                initialData={soilCardResponse.soilData}
                validationErrors={soilCardResponse.validationErrors}
                onSubmit={handleManualDataSubmit}
                onCancel={() => setViewMode('upload')}
                isLoading={isProcessing}
              />
            )}
          </div>
        )}

        {/* History View */}
        {viewMode === 'history' && (
          <div className="space-y-6">
            {isLoadingHistory ? (
              <div className="flex justify-center py-12">
                <LoadingSpinner size="lg" />
              </div>
            ) : soilHistory.length > 0 ? (
              <>
                <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
                  <SoilHistoryChart history={soilHistory} nutrient="pH" />
                  <SoilHistoryChart history={soilHistory} nutrient="organicCarbon" />
                  <SoilHistoryChart history={soilHistory} nutrient="nitrogen" />
                  <SoilHistoryChart history={soilHistory} nutrient="phosphorus" />
                </div>

                <div className="bg-white dark:bg-gray-800 rounded-lg shadow-md p-6">
                  <h3 className="text-lg font-semibold text-gray-900 dark:text-gray-100 mb-4">
                    {t('soilAnalysis.recentSamples', 'Recent Samples')}
                  </h3>
                  <div className="space-y-4">
                    {soilHistory.slice(0, 5).map((sample) => (
                      <div
                        key={sample.sampleId}
                        className="border border-gray-200 dark:border-gray-700 rounded-lg p-4 hover:bg-gray-50 dark:hover:bg-gray-700/50 transition-colors"
                      >
                        <div className="flex justify-between items-start">
                          <div>
                            <p className="font-medium text-gray-900 dark:text-gray-100">
                              {sample.sampleId}
                            </p>
                            <p className="text-sm text-gray-500 dark:text-gray-400">
                              {new Date(sample.collectionDate).toLocaleDateString()}
                            </p>
                          </div>
                          <span className="text-sm text-gray-600 dark:text-gray-400">
                            pH: {(sample.pH ?? 0).toFixed(1)}
                          </span>
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              </>
            ) : (
              <div className="bg-white dark:bg-gray-800 rounded-lg shadow-md p-12 text-center">
                <p className="text-gray-500 dark:text-gray-400">
                  {t('soilAnalysis.noHistory', 'No soil analysis history available')}
                </p>
                <button
                  onClick={() => setViewMode('upload')}
                  className="mt-4 px-6 py-2 bg-green-600 text-white rounded-md hover:bg-green-700"
                >
                  {t('soilAnalysis.uploadFirst', 'Upload Your First Card')}
                </button>
              </div>
            )}
          </div>
        )}

        {/* Plan View */}
        {viewMode === 'plan' && regenerativePlan && (
          <RegenerativePlanDisplay
            plan={regenerativePlan}
            onSave={handleSavePlan}
          />
        )}

        {/* Saved Plans View */}
        {viewMode === 'saved' && (
          <SavedPlansView
            onSelectPlan={(plan) => {
              setRegenerativePlan(plan);
              setViewMode('plan');
            }}
          />
        )}
      </div>
    </div>
  );
};
