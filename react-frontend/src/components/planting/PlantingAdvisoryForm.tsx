import React, { useState, useEffect } from 'react';
import { useLanguage } from '@/contexts/LanguageContext';
import { soilAnalysisService, type RegenerativePlan } from '@/services/soilAnalysisService';

interface PlantingAdvisoryFormProps {
  onSubmit: (cropType: string, location: string, forecastDays: number, planId?: string) => void;
  isLoading: boolean;
}

const COMMON_CROPS = [
  { value: 'wheat', label: 'Wheat', labelHi: 'गेहूं' },
  { value: 'rice', label: 'Rice', labelHi: 'चावल' },
  { value: 'cotton', label: 'Cotton', labelHi: 'कपास' },
  { value: 'maize', label: 'Maize', labelHi: 'मक्का' },
  { value: 'sugarcane', label: 'Sugarcane', labelHi: 'गन्ना' },
  { value: 'soybean', label: 'Soybean', labelHi: 'सोयाबीन' },
  { value: 'pulses', label: 'Pulses', labelHi: 'दालें' },
  { value: 'potato', label: 'Potato', labelHi: 'आलू' },
  { value: 'tomato', label: 'Tomato', labelHi: 'टमाटर' },
  { value: 'onion', label: 'Onion', labelHi: 'प्याज' },
  { value: 'cabbage', label: 'Cabbage', labelHi: 'पत्तागोभी' },
  { value: 'cauliflower', label: 'Cauliflower', labelHi: 'फूलगोभी' },
  { value: 'brinjal', label: 'Brinjal (Eggplant)', labelHi: 'बैंगन' },
  { value: 'okra', label: 'Okra (Ladyfinger)', labelHi: 'भिंडी' },
  { value: 'vegetables-mixed', label: 'Mixed Vegetables', labelHi: 'मिश्रित सब्जियां' }
];

export const PlantingAdvisoryForm: React.FC<PlantingAdvisoryFormProps> = ({
  onSubmit,
  isLoading
}) => {
  const { currentLanguage } = useLanguage();
  const language = currentLanguage.code;
  const [cropType, setCropType] = useState('');
  const [location, setLocation] = useState('');
  const [forecastDays, setForecastDays] = useState(90);
  const [selectedPlanId, setSelectedPlanId] = useState<string>('');
  const [savedPlans, setSavedPlans] = useState<RegenerativePlan[]>([]);
  const [loadingPlans, setLoadingPlans] = useState(false);

  useEffect(() => {
    loadSavedPlans();
  }, []);

  const loadSavedPlans = async () => {
    try {
      setLoadingPlans(true);
      const plans = await soilAnalysisService.getSavedPlans();
      setSavedPlans(plans);
    } catch (error) {
      console.error('Failed to load saved plans:', error);
    } finally {
      setLoadingPlans(false);
    }
  };

  const handlePlanSelection = (planId: string) => {
    setSelectedPlanId(planId);
    if (planId) {
      const plan = savedPlans.find(p => p.planId === planId);
      if (plan) {
        // Auto-fill location from plan if available
        // Note: Backend will extract soil data from the plan
      }
    }
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (cropType && location) {
      onSubmit(cropType, location, forecastDays, selectedPlanId || undefined);
    }
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-6">
      {/* Saved Plans Dropdown */}
      <div>
        <label htmlFor="savedPlan" className="block text-sm font-medium text-gray-700 mb-2">
          {language === 'hi' ? 'सहेजी गई मिट्टी योजना (वैकल्पिक)' : 'Saved Soil Plan (Optional)'}
        </label>
        <select
          id="savedPlan"
          value={selectedPlanId}
          onChange={(e) => handlePlanSelection(e.target.value)}
          className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-green-500 focus:border-transparent"
          disabled={isLoading || loadingPlans}
        >
          <option value="">
            {language === 'hi' ? 'कोई योजना नहीं - नई मिट्टी डेटा का उपयोग करें' : 'No Plan - Use New Soil Data'}
          </option>
          {savedPlans.map((plan) => (
            <option key={plan.planId} value={plan.planId}>
              {language === 'hi' ? 'योजना' : 'Plan'} #{plan.planId.slice(0, 8)} - {new Date(plan.createdAt).toLocaleDateString()}
            </option>
          ))}
        </select>
        {selectedPlanId && (
          <p className="mt-2 text-sm text-green-600">
            {language === 'hi' 
              ? '✓ चयनित योजना से मिट्टी डेटा का उपयोग किया जाएगा' 
              : '✓ Soil data from selected plan will be used'}
          </p>
        )}
      </div>

      <div>
        <label htmlFor="cropType" className="block text-sm font-medium text-gray-700 mb-2">
          {language === 'hi' ? 'फसल का प्रकार' : 'Crop Type'}
          <span className="text-red-500 ml-1">*</span>
        </label>
        <select
          id="cropType"
          value={cropType}
          onChange={(e) => setCropType(e.target.value)}
          className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-green-500 focus:border-transparent"
          required
          disabled={isLoading}
        >
          <option value="">
            {language === 'hi' ? 'फसल चुनें' : 'Select Crop'}
          </option>
          {COMMON_CROPS.map((crop) => (
            <option key={crop.value} value={crop.value}>
              {language === 'hi' ? crop.labelHi : crop.label}
            </option>
          ))}
        </select>
      </div>

      <div>
        <label htmlFor="location" className="block text-sm font-medium text-gray-700 mb-2">
          {language === 'hi' ? 'स्थान (जिला/शहर)' : 'Location (District/City)'}
          <span className="text-red-500 ml-1">*</span>
        </label>
        <input
          type="text"
          id="location"
          value={location}
          onChange={(e) => setLocation(e.target.value)}
          placeholder={language === 'hi' ? 'उदाहरण: पुणे, महाराष्ट्र' : 'e.g., Pune, Maharashtra'}
          className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-green-500 focus:border-transparent"
          required
          disabled={isLoading}
        />
      </div>

      <div>
        <label htmlFor="forecastDays" className="block text-sm font-medium text-gray-700 mb-2">
          {language === 'hi' ? 'पूर्वानुमान अवधि (दिन)' : 'Forecast Period (Days)'}
        </label>
        <input
          type="number"
          id="forecastDays"
          value={forecastDays}
          onChange={(e) => setForecastDays(parseInt(e.target.value) || 90)}
          min="1"
          max="90"
          className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-green-500 focus:border-transparent"
          disabled={isLoading}
        />
        <p className="mt-1 text-sm text-gray-500">
          {language === 'hi' 
            ? '1 से 90 दिनों के बीच (डिफ़ॉल्ट: 90)' 
            : 'Between 1 and 90 days (Default: 90)'}
        </p>
      </div>

      <button
        type="submit"
        disabled={isLoading || !cropType || !location}
        className="w-full bg-green-600 text-white py-3 px-6 rounded-lg font-medium hover:bg-green-700 disabled:bg-gray-400 disabled:cursor-not-allowed transition-colors"
      >
        {isLoading 
          ? (language === 'hi' ? 'सिफारिशें प्राप्त कर रहे हैं...' : 'Getting Recommendations...') 
          : (language === 'hi' ? 'सिफारिशें प्राप्त करें' : 'Get Recommendations')}
      </button>
    </form>
  );
};
