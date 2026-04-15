import React from 'react';
import { TrendingUp, MapPin, Calendar, DollarSign, Play, Square } from 'lucide-react';
import { useLanguage } from '@/contexts/LanguageContext';
import { VoiceQueryResponse } from '@/types';
import { AudioPlayer } from './AudioPlayer';

interface VoiceQueryResultsProps {
  result: VoiceQueryResponse;
  onAddToFavorites?: () => void;
}

export const VoiceQueryResults: React.FC<VoiceQueryResultsProps> = ({
  result,
  onAddToFavorites
}) => {
  const { t } = useLanguage();
  const [isSpeaking, setIsSpeaking] = React.useState(false);
  const speechSynthesisRef = React.useRef<SpeechSynthesisUtterance | null>(null);

  // Load voices on mount (some browsers need this)
  React.useEffect(() => {
    if ('speechSynthesis' in window) {
      // Load voices
      const loadVoices = () => {
        window.speechSynthesis.getVoices();
      };

      // Chrome loads voices asynchronously
      if (window.speechSynthesis.onvoiceschanged !== undefined) {
        window.speechSynthesis.onvoiceschanged = loadVoices;
      }
      
      // Try loading immediately
      loadVoices();
    }
  }, []);

  // Cleanup speech synthesis on unmount
  React.useEffect(() => {
    return () => {
      if (speechSynthesisRef.current) {
        window.speechSynthesis.cancel();
      }
    };
  }, []);

  // Clean markdown formatting from text for speech
  const cleanTextForSpeech = (text: string): string => {
    return text
      // Remove markdown headers (###, ##, #)
      .replace(/^#{1,6}\s+/gm, '')
      // Remove bold/italic markers (**, *, __, _)
      .replace(/(\*\*|__)(.*?)\1/g, '$2')
      .replace(/(\*|_)(.*?)\1/g, '$2')
      // Remove strikethrough (~~)
      .replace(/~~(.*?)~~/g, '$1')
      // Remove inline code (`code`)
      .replace(/`([^`]+)`/g, '$1')
      // Remove code blocks (```code```)
      .replace(/```[\s\S]*?```/g, '')
      // Remove links but keep text [text](url)
      .replace(/\[([^\]]+)\]\([^\)]+\)/g, '$1')
      // Remove images ![alt](url)
      .replace(/!\[([^\]]*)\]\([^\)]+\)/g, '')
      // Remove horizontal rules (---, ***)
      .replace(/^[-*_]{3,}\s*$/gm, '')
      // Remove blockquotes (>)
      .replace(/^>\s+/gm, '')
      // Remove list markers (-, *, +, 1.)
      .replace(/^[\s]*[-*+]\s+/gm, '')
      .replace(/^[\s]*\d+\.\s+/gm, '')
      // Remove extra whitespace and newlines
      .replace(/\n{3,}/g, '\n\n')
      .trim();
  };

  const handleTextToSpeech = () => {
    if (!result.responseText) {

      return;
    }

    // Check if Speech Synthesis is supported
    if (!('speechSynthesis' in window)) {
      console.error('Speech Synthesis not supported in this browser');
      alert('Text-to-speech is not supported in your browser');
      return;
    }

    // If already speaking, stop
    if (isSpeaking) {

      window.speechSynthesis.cancel();
      setIsSpeaking(false);
      return;
    }

    // Clean the text before speaking
    const cleanedText = cleanTextForSpeech(result.responseText);
    
    // Get available voices
    const voices = window.speechSynthesis.getVoices();

    // Create speech synthesis utterance with cleaned text
    const utterance = new SpeechSynthesisUtterance(cleanedText);
    speechSynthesisRef.current = utterance;

    // Set language based on dialect with fallback
    const langMap: { [key: string]: string } = {
      'hi-IN': 'hi-IN',
      'en-IN': 'en-IN',
      'pa-IN': 'pa-IN',
      'mr-IN': 'mr-IN',
      'gu-IN': 'gu-IN',
      'ta-IN': 'ta-IN',
      'te-IN': 'te-IN',
      'kn-IN': 'kn-IN',
      'ml-IN': 'ml-IN',
      'bn-IN': 'bn-IN',
    };
    
    const preferredLang = langMap[result.dialect] || 'en-IN';
    
    // Try to find a voice for the preferred language
    let selectedVoice = voices.find(voice => voice.lang === preferredLang);
    
    // Fallback to any Indian English voice
    if (!selectedVoice) {
      selectedVoice = voices.find(voice => voice.lang.startsWith('en-IN'));
    }
    
    // Fallback to any English voice
    if (!selectedVoice) {
      selectedVoice = voices.find(voice => voice.lang.startsWith('en'));
    }
    
    // Use the first available voice as last resort
    if (!selectedVoice && voices.length > 0) {
      selectedVoice = voices[0];
    }

    if (selectedVoice) {
      utterance.voice = selectedVoice;
      utterance.lang = selectedVoice.lang;

    } else {
      utterance.lang = 'en-US'; // Fallback language

    }

    utterance.rate = 0.95; // Slightly slower for better clarity
    utterance.pitch = 1.0; // Natural pitch
    utterance.volume = 1.0;

    // Event handlers
    utterance.onstart = () => {

      setIsSpeaking(true);
    };
    utterance.onend = () => {

      setIsSpeaking(false);
      speechSynthesisRef.current = null;
    };
    utterance.onerror = (event) => {
      console.error('Speech synthesis error:', event.error);
      setIsSpeaking(false);
      speechSynthesisRef.current = null;
      
      // Don't show alert for 'interrupted' or 'canceled' errors (happens on page navigation)
      if (event.error !== 'interrupted' && event.error !== 'canceled') {
        console.error('Speech error details:', event);
      }
    };

    // Speak
    try {
      window.speechSynthesis.speak(utterance);

    } catch (error) {
      console.error('Error starting speech:', error);
      alert('Failed to start text-to-speech');
    }
  };

  const formatPrice = (price: number): string => {
    return new Intl.NumberFormat('en-IN', {
      style: 'currency',
      currency: 'INR',
      maximumFractionDigits: 0
    }).format(price);
  };

  const formatDate = (dateString: string): string => {
    try {
      const date = new Date(dateString);
      
      // Check if date is valid
      if (isNaN(date.getTime())) {
        return t('voice.unknownDate', 'Unknown date');
      }
      
      return new Intl.DateTimeFormat('en-IN', {
        day: 'numeric',
        month: 'short',
        year: 'numeric'
      }).format(date);
    } catch (error) {
      console.error('Error formatting date:', dateString, error);
      return t('voice.unknownDate', 'Unknown date');
    }
  };

  return (
    <div className="space-y-6">
      {/* Transcription */}
      <div className="bg-blue-50 dark:bg-blue-900/20 border border-blue-200 dark:border-blue-800 rounded-lg p-4">
        <h3 className="text-sm font-medium text-blue-900 dark:text-blue-100 mb-2">
          {t('voice.yourQuery', 'Your Query')}
        </h3>
        <p className="text-blue-800 dark:text-blue-200">
          "{result.transcription}"
        </p>
        <div className="mt-2 flex items-center justify-between">
          <span className="text-xs text-blue-600 dark:text-blue-400">
            {t('voice.confidence', 'Confidence')}: {Math.round(result.confidence * 100)}%
          </span>
          <span className="text-xs text-blue-600 dark:text-blue-400">
            {t('voice.dialect', 'Dialect')}: {result.dialect}
          </span>
        </div>
      </div>

      {/* Audio Response - Only show if valid URL exists */}
      {result.audioResponseUrl && 
       typeof result.audioResponseUrl === 'string' && 
       result.audioResponseUrl.trim() !== '' && 
       result.audioResponseUrl.startsWith('http') && (
        <div>
          <h3 className="text-sm font-medium text-gray-900 dark:text-gray-100 mb-3">
            {t('voice.audioResponse', 'Audio Response')}
          </h3>
          <AudioPlayer 
            audioUrl={result.audioResponseUrl} 
            autoPlay={false}
            hideOnError={true}
          />
        </div>
      )}

      {/* AI Response Text (for general questions) */}
      {result.responseText && (
        <div className="bg-green-50 dark:bg-green-900/20 border border-green-200 dark:border-green-800 rounded-lg p-4">
          <div className="flex items-center justify-between mb-2">
            <h3 className="text-sm font-medium text-green-900 dark:text-green-100">
              {t('voice.aiResponse', 'AI Response')}
            </h3>
            <button
              onClick={handleTextToSpeech}
              className="flex items-center gap-1 px-3 py-1 text-sm text-green-700 dark:text-green-300 hover:bg-green-100 dark:hover:bg-green-800/50 rounded-md transition-colors"
              aria-label={isSpeaking ? t('voice.stopSpeech', 'Stop speech') : t('voice.playSpeech', 'Play speech')}
            >
              {isSpeaking ? (
                <>
                  <Square className="w-4 h-4" />
                  <span>{t('voice.stop', 'Stop')}</span>
                </>
              ) : (
                <>
                  <Play className="w-4 h-4" />
                  <span>{t('voice.play', 'Play')}</span>
                </>
              )}
            </button>
          </div>
          <p className="text-green-800 dark:text-green-200 whitespace-pre-wrap">
            {result.responseText}
          </p>
        </div>
      )}

      {/* Market Prices */}
      {result.prices && result.prices.length > 0 && (
        <div>
          <div className="flex items-center justify-between mb-3">
            <h3 className="text-lg font-semibold text-gray-900 dark:text-gray-100">
              {t('voice.marketPrices', 'Market Prices')}
            </h3>
            {onAddToFavorites && (
              <button
                onClick={onAddToFavorites}
                className="text-sm text-green-600 dark:text-green-400 hover:text-green-700 dark:hover:text-green-300"
              >
                {t('voice.addToFavorites', 'Add to Favorites')}
              </button>
            )}
          </div>

          <div className="grid gap-4 md:grid-cols-2">
            {result.prices.map((price, index) => (
              <div
                key={index}
                className="bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg p-4 hover:shadow-md transition-shadow"
              >
                {/* Commodity Name */}
                <div className="flex items-start justify-between mb-3">
                  <h4 className="text-lg font-semibold text-gray-900 dark:text-gray-100">
                    {price.commodity}
                  </h4>
                  <div className="flex items-center text-green-600 dark:text-green-400">
                    <TrendingUp className="w-4 h-4 mr-1" />
                  </div>
                </div>

                {/* Price */}
                <div className="mb-3">
                  <div className="flex items-baseline">
                    <span className="text-3xl font-bold text-gray-900 dark:text-gray-100">
                      {formatPrice(price.price)}
                    </span>
                    <span className="ml-2 text-sm text-gray-500 dark:text-gray-400">
                      / {price.unit}
                    </span>
                  </div>
                </div>

                {/* Market Info */}
                <div className="space-y-2">
                  <div className="flex items-center text-sm text-gray-600 dark:text-gray-400">
                    <MapPin className="w-4 h-4 mr-2" />
                    <span>{price.market}</span>
                  </div>
                  <div className="flex items-center text-sm text-gray-600 dark:text-gray-400">
                    <Calendar className="w-4 h-4 mr-2" />
                    <span>{formatDate(price.date)}</span>
                  </div>
                  <div className="flex items-center text-sm text-gray-600 dark:text-gray-400">
                    <DollarSign className="w-4 h-4 mr-2" />
                    <span>{t('voice.source', 'Source')}: {price.source}</span>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* No Results */}
      {(!result.prices || result.prices.length === 0) && !result.responseText && (
        <div className="bg-yellow-50 dark:bg-yellow-900/20 border border-yellow-200 dark:border-yellow-800 rounded-lg p-4">
          <p className="text-yellow-800 dark:text-yellow-200">
            {t('voice.noPricesFound', 'No market prices found for your query. Please try again with a different query.')}
          </p>
        </div>
      )}
    </div>
  );
};
