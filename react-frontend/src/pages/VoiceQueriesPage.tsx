import React, { useState, useEffect } from 'react';
import { Mic, AlertCircle, Wifi, WifiOff } from 'lucide-react';
import { useLanguage } from '@/contexts/LanguageContext';
import { VoiceRecorder } from '@/components/voice/VoiceRecorder';
import { DialectSelector } from '@/components/voice/DialectSelector';
import { VoiceQueryResults } from '@/components/voice/VoiceQueryResults';
import { QueryHistory } from '@/components/voice/QueryHistory';
import { voiceQueryService, VoiceQueryHistoryItem } from '@/services/voiceQueryService';
import { uploadQueueService } from '@/services/uploadQueueService';
import { useNotifications } from '@/hooks/useNotifications';
import { Dialect, VoiceQueryResponse } from '@/types';
import { LoadingSpinner } from '@/components/common/LoadingSpinner';

interface QueryHistoryItem {
  id: string;
  query: string;
  response: VoiceQueryResponse;
  timestamp: string;
  isFavorite: boolean;
}

export const VoiceQueriesPage: React.FC = () => {
  const { t } = useLanguage();
  const { showNotification } = useNotifications();
  
  const [availableDialects, setAvailableDialects] = useState<Dialect[]>([]);
  const [selectedDialect, setSelectedDialect] = useState<string>('hi-IN');
  const [isProcessing, setIsProcessing] = useState(false);
  const [currentResult, setCurrentResult] = useState<VoiceQueryResponse | null>(null);
  const [queryHistory, setQueryHistory] = useState<QueryHistoryItem[]>([]);
  const [isOnline, setIsOnline] = useState(navigator.onLine);
  const [isLoadingDialects, setIsLoadingDialects] = useState(true);
  const [isLoadingHistory, setIsLoadingHistory] = useState(true);

  // Load dialects on mount
  useEffect(() => {
    loadDialects();
    loadHistory();

    // Listen for online/offline events
    const handleOnline = () => {
      setIsOnline(true);
      loadHistory(); // Refresh history when coming back online
    };
    const handleOffline = () => setIsOnline(false);

    window.addEventListener('online', handleOnline);
    window.addEventListener('offline', handleOffline);

    return () => {
      window.removeEventListener('online', handleOnline);
      window.removeEventListener('offline', handleOffline);
    };
  }, []);

  const loadDialects = async () => {
    try {
      setIsLoadingDialects(true);
      const dialects = await voiceQueryService.getSupportedDialects();
      setAvailableDialects(dialects);
    } catch (error) {
      console.error('Failed to load dialects:', error);
      showNotification({
        type: 'error',
        title: t('voice.error', 'Error'),
        message: t('voice.dialectLoadError', 'Failed to load dialects. Using defaults.')
      });
    } finally {
      setIsLoadingDialects(false);
    }
  };

  const loadHistory = async () => {
    try {
      setIsLoadingHistory(true);
      const history = await voiceQueryService.getHistory(50);
      
      // Convert backend format to frontend format
      const formattedHistory: QueryHistoryItem[] = history.map(item => ({
        id: item.queryId,
        query: item.transcription,
        response: {
          transcription: item.transcription,
          responseText: item.responseText,
          audioResponseUrl: '', // Will be generated on demand
          confidence: item.confidence,
          dialect: item.dialect,
          prices: item.prices
        },
        timestamp: item.timestamp,
        isFavorite: item.isFavorite
      }));

      setQueryHistory(formattedHistory);
    } catch (error) {
      console.error('Failed to load history:', error);
      // Fallback to localStorage if backend fails
      loadHistoryFromStorage();
    } finally {
      setIsLoadingHistory(false);
    }
  };

  const loadHistoryFromStorage = () => {
    try {
      const stored = localStorage.getItem('voiceQueryHistory');
      if (stored) {
        setQueryHistory(JSON.parse(stored));
      }
    } catch (error) {
      console.error('Failed to load history from storage:', error);
    }
  };

  const handleRecordingComplete = async (audioBlob: Blob) => {
    try {
      // Validate audio
      const audioFile = await voiceQueryService.convertAudioFormat(audioBlob, 'mp3');
      const validation = voiceQueryService.validateAudioFile(audioFile);

      if (!validation.isValid) {
        showNotification({
          type: 'error',
          title: t('voice.error', 'Error'),
          message: validation.error || t('voice.invalidAudio', 'Invalid audio file')
        });
        return;
      }

      setIsProcessing(true);
      setCurrentResult(null);

      if (!isOnline) {
        // Queue for offline processing
        uploadQueueService.addToQueue(
          audioFile,
          '/api/voice-query',
          'current-user', // Replace with actual farmer ID
          'high'
        );

        showNotification({
          type: 'info',
          title: t('voice.offline', 'Offline'),
          message: t('voice.queuedForSync', 'Voice query queued for processing when online')
        });

        setIsProcessing(false);
        return;
      }

      // Process voice query (backend automatically saves to history)
      const result = await voiceQueryService.processVoiceQuery(audioFile, selectedDialect);
      setCurrentResult(result);

      // Reload history from backend to get the new query
      await loadHistory();

      showNotification({
        type: 'success',
        title: t('voice.success', 'Success'),
        message: t('voice.queryProcessed', 'Voice query processed successfully')
      });
    } catch (error) {
      console.error('Voice query failed:', error);
      showNotification({
        type: 'error',
        title: t('voice.error', 'Error'),
        message: error instanceof Error ? error.message : t('voice.processingFailed', 'Failed to process voice query')
      });
    } finally {
      setIsProcessing(false);
    }
  };

  const handleRecordingError = (error: string) => {
    showNotification({
      type: 'error',
      title: t('voice.error', 'Error'),
      message: error
    });
  };

  const handleSelectQuery = (item: QueryHistoryItem) => {
    setCurrentResult(item.response);
  };

  const handleToggleFavorite = async (id: string) => {
    try {
      const item = queryHistory.find(q => q.id === id);
      if (!item) return;

      const newFavoriteStatus = !item.isFavorite;
      
      // Optimistic update
      const updatedHistory = queryHistory.map(q =>
        q.id === id ? { ...q, isFavorite: newFavoriteStatus } : q
      );
      setQueryHistory(updatedHistory);

      // Update backend
      await voiceQueryService.toggleFavorite(id, newFavoriteStatus);
    } catch (error) {
      console.error('Failed to toggle favorite:', error);
      // Revert on error
      await loadHistory();
      showNotification({
        type: 'error',
        title: t('voice.error', 'Error'),
        message: t('voice.favoriteError', 'Failed to update favorite status')
      });
    }
  };

  const handleDeleteQuery = async (id: string) => {
    try {
      // Optimistic update
      const updatedHistory = queryHistory.filter(item => item.id !== id);
      setQueryHistory(updatedHistory);

      // Clear current result if it's the deleted query
      if (currentResult && queryHistory.find(item => item.id === id)?.response === currentResult) {
        setCurrentResult(null);
      }

      // Delete from backend
      await voiceQueryService.deleteQuery(id);

      showNotification({
        type: 'success',
        title: t('voice.deleted', 'Deleted'),
        message: t('voice.queryDeleted', 'Query deleted from history')
      });
    } catch (error) {
      console.error('Failed to delete query:', error);
      // Revert on error
      await loadHistory();
      showNotification({
        type: 'error',
        title: t('voice.error', 'Error'),
        message: t('voice.deleteError', 'Failed to delete query')
      });
    }
  };

  const handleAddToFavorites = () => {
    if (!currentResult) return;

    const currentQuery = queryHistory.find(item => item.response === currentResult);
    if (currentQuery && !currentQuery.isFavorite) {
      handleToggleFavorite(currentQuery.id);
      showNotification({
        type: 'success',
        title: t('voice.added', 'Added'),
        message: t('voice.addedToFavorites', 'Added to favorites')
      });
    }
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="bg-white dark:bg-gray-800 rounded-lg shadow-md p-6">
        <div className="flex items-center justify-between mb-4">
          <div className="flex items-center space-x-3">
            <div className="w-12 h-12 bg-green-100 dark:bg-green-900/30 rounded-lg flex items-center justify-center">
              <Mic className="w-6 h-6 text-green-600 dark:text-green-400" />
            </div>
            <div>
              <h1 className="text-3xl font-bold text-gray-900 dark:text-gray-100">
                {t('nav.voiceQueries', 'Voice Queries')}
              </h1>
              <p className="text-sm text-gray-600 dark:text-gray-400">
                {t('voice.subtitle', 'Ask about market prices using your voice')}
              </p>
            </div>
          </div>

          {/* Online/Offline Indicator */}
          <div className={`flex items-center space-x-2 px-3 py-1.5 rounded-lg ${
            isOnline
              ? 'bg-green-100 dark:bg-green-900/30 text-green-700 dark:text-green-300'
              : 'bg-red-100 dark:bg-red-900/30 text-red-700 dark:text-red-300'
          }`}>
            {isOnline ? (
              <>
                <Wifi className="w-4 h-4" />
                <span className="text-sm font-medium">{t('voice.online', 'Online')}</span>
              </>
            ) : (
              <>
                <WifiOff className="w-4 h-4" />
                <span className="text-sm font-medium">{t('voice.offline', 'Offline')}</span>
              </>
            )}
          </div>
        </div>

        {/* Offline Warning */}
        {!isOnline && (
          <div className="bg-yellow-50 dark:bg-yellow-900/20 border border-yellow-200 dark:border-yellow-800 rounded-lg p-4 flex items-start space-x-3">
            <AlertCircle className="w-5 h-5 text-yellow-600 dark:text-yellow-400 flex-shrink-0 mt-0.5" />
            <div>
              <p className="text-sm text-yellow-800 dark:text-yellow-200">
                {t('voice.offlineWarning', 'You are offline. Voice queries will be queued and processed when connection is restored.')}
              </p>
            </div>
          </div>
        )}
      </div>

      {/* Main Content */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Left Column - Recording Interface */}
        <div className="lg:col-span-2 space-y-6">
          {/* Dialect Selection */}
          {isLoadingDialects ? (
            <div className="bg-white dark:bg-gray-800 rounded-lg shadow-md p-6">
              <LoadingSpinner size="sm" />
            </div>
          ) : (
            <DialectSelector
              selectedDialect={selectedDialect}
              onDialectChange={setSelectedDialect}
              availableDialects={availableDialects}
            />
          )}

          {/* Voice Recorder */}
          <VoiceRecorder
            maxDuration={60}
            onRecordingComplete={handleRecordingComplete}
            onError={handleRecordingError}
            dialect={selectedDialect}
          />

          {/* Processing Indicator */}
          {isProcessing && (
            <div className="bg-white dark:bg-gray-800 rounded-lg shadow-md p-6">
              <div className="flex items-center justify-center space-x-3">
                <LoadingSpinner size="sm" />
                <p className="text-gray-600 dark:text-gray-400">
                  {t('voice.processing', 'Processing your voice query...')}
                </p>
              </div>
            </div>
          )}

          {/* Results */}
          {currentResult && !isProcessing && (
            <VoiceQueryResults
              result={currentResult}
              onAddToFavorites={handleAddToFavorites}
            />
          )}
        </div>

        {/* Right Column - History */}
        <div className="lg:col-span-1">
          {isLoadingHistory ? (
            <div className="bg-white dark:bg-gray-800 rounded-lg shadow-md p-6">
              <LoadingSpinner size="sm" />
              <p className="text-center text-gray-600 dark:text-gray-400 mt-2">
                {t('voice.loadingHistory', 'Loading history...')}
              </p>
            </div>
          ) : (
            <QueryHistory
              history={queryHistory}
              onSelectQuery={handleSelectQuery}
              onToggleFavorite={handleToggleFavorite}
              onDeleteQuery={handleDeleteQuery}
            />
          )}
        </div>
      </div>
    </div>
  );
};
