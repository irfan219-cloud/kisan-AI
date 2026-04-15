import React, { createContext, useContext, useEffect } from 'react';
import { useSelector, useDispatch } from 'react-redux';
import { useTranslation } from 'react-i18next';
import { RootState } from '@/store';
import { setLanguage } from '@/store/slices/appSlice';
import { applyLanguageFont } from '@/utils/languageFonts';

export interface Language {
  code: string;
  name: string;
  nativeName: string;
  direction: 'ltr' | 'rtl';
}

export const supportedLanguages: Language[] = [
  { code: 'en', name: 'English', nativeName: 'English', direction: 'ltr' },
  { code: 'hi', name: 'Hindi', nativeName: 'हिन्दी', direction: 'ltr' },
  { code: 'bn', name: 'Bengali', nativeName: 'বাংলা', direction: 'ltr' },
  { code: 'te', name: 'Telugu', nativeName: 'తెలుగు', direction: 'ltr' },
  { code: 'mr', name: 'Marathi', nativeName: 'मराठी', direction: 'ltr' },
  { code: 'ta', name: 'Tamil', nativeName: 'தமிழ்', direction: 'ltr' },
  { code: 'gu', name: 'Gujarati', nativeName: 'ગુજરાતી', direction: 'ltr' },
  { code: 'kn', name: 'Kannada', nativeName: 'ಕನ್ನಡ', direction: 'ltr' },
];

interface LanguageContextType {
  currentLanguage: Language;
  changeLanguage: (languageCode: string) => void;
  supportedLanguages: Language[];
  t: (key: string, options?: any) => string;
}

const LanguageContext = createContext<LanguageContextType | undefined>(undefined);

export const useLanguage = () => {
  const context = useContext(LanguageContext);
  if (context === undefined) {
    throw new Error('useLanguage must be used within a LanguageProvider');
  }
  return context;
};

interface LanguageProviderProps {
  children: React.ReactNode;
}

export const LanguageProvider: React.FC<LanguageProviderProps> = ({ children }) => {
  const dispatch = useDispatch();
  const languageCode = useSelector((state: RootState) => state.app.language);
  const { i18n, t } = useTranslation();

  const currentLanguage = supportedLanguages.find(lang => lang.code === languageCode) || supportedLanguages[0];

  useEffect(() => {
    // Load language from localStorage on mount
    const savedLanguage = localStorage.getItem('language');
    if (savedLanguage && supportedLanguages.some(lang => lang.code === savedLanguage)) {
      dispatch(setLanguage(savedLanguage));
      i18n.changeLanguage(savedLanguage);
    }
  }, [dispatch, i18n]);

  useEffect(() => {
    // Sync i18next with current language
    if (i18n.language !== languageCode) {
      i18n.changeLanguage(languageCode);
    }

    // Apply language direction to document
    document.documentElement.dir = currentLanguage.direction;
    document.documentElement.lang = currentLanguage.code;
    
    // Apply appropriate font for the language
    applyLanguageFont(currentLanguage.code);
    
    // Persist language preference
    localStorage.setItem('language', currentLanguage.code);
  }, [currentLanguage, languageCode, i18n]);

  const changeLanguage = (newLanguageCode: string) => {
    if (supportedLanguages.some(lang => lang.code === newLanguageCode)) {
      dispatch(setLanguage(newLanguageCode));
      i18n.changeLanguage(newLanguageCode);
    }
  };

  return (
    <LanguageContext.Provider value={{
      currentLanguage,
      changeLanguage,
      supportedLanguages,
      t,
    }}>
      {children}
    </LanguageContext.Provider>
  );
};