import React, { useState, useRef, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '@/contexts/AuthContext';
import { useLanguage } from '@/contexts/LanguageContext';
import { Bars3Icon, XMarkIcon, UserCircleIcon, ChevronDownIcon } from '@heroicons/react/24/outline';

interface HeaderProps {
  onMenuToggle: () => void;
  isMobileMenuOpen: boolean;
}

export const Header: React.FC<HeaderProps> = ({ onMenuToggle, isMobileMenuOpen }) => {
  const { user, logout } = useAuth();
  const { t } = useLanguage();
  const [isUserMenuOpen, setIsUserMenuOpen] = useState(false);
  const userMenuRef = useRef<HTMLDivElement>(null);

  // Close dropdown when clicking outside
  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (userMenuRef.current && !userMenuRef.current.contains(event.target as Node)) {
        setIsUserMenuOpen(false);
      }
    };

    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  return (
    <header 
      className="bg-primary-600 dark:bg-primary-700 text-white shadow-md sticky top-0 z-40"
      role="banner"
    >
      <div className="px-2 sm:px-4 lg:px-6">
        <div className="flex items-center justify-between h-16">
          {/* Logo and Brand */}
          <div className="flex items-center space-x-4">
            <button
              onClick={onMenuToggle}
              className="md:hidden p-2 rounded-md hover:bg-primary-700 dark:hover:bg-primary-600 transition-colors focus:outline-none focus:ring-2 focus:ring-white focus:ring-offset-2 focus:ring-offset-primary-600"
              aria-label={isMobileMenuOpen ? t('nav.closeMenu', 'Close menu') : t('nav.openMenu', 'Open menu')}
              aria-expanded={isMobileMenuOpen}
              aria-controls="mobile-navigation"
            >
              {isMobileMenuOpen ? (
                <XMarkIcon className="h-6 w-6" aria-hidden="true" />
              ) : (
                <Bars3Icon className="h-6 w-6" aria-hidden="true" />
              )}
            </button>
            
            <Link 
              to="/dashboard" 
              className="flex items-center space-x-2 focus:outline-none focus:ring-2 focus:ring-white focus:ring-offset-2 focus:ring-offset-primary-600 rounded-md"
              aria-label={t('header.homeLink', 'KisanMitra AI - Go to dashboard')}
            >
              <span className="text-2xl" role="img" aria-label="Wheat emoji">🌾</span>
              <div className="hidden sm:block">
                <h1 className="text-xl font-bold">KisanMitra AI</h1>
                <p className="text-xs text-primary-100">{t('header.tagline', 'Your Farming Assistant')}</p>
              </div>
            </Link>
          </div>

          {/* Desktop Navigation */}
          <nav 
            className="hidden md:flex items-center space-x-6"
            role="navigation"
            aria-label={t('nav.mainNavigation', 'Main navigation')}
          >
            <Link
              to="/dashboard"
              className="text-white hover:text-primary-100 transition-colors font-medium focus:outline-none focus:ring-2 focus:ring-white focus:ring-offset-2 focus:ring-offset-primary-600 rounded px-2 py-1"
            >
              {t('nav.dashboard', 'Dashboard')}
            </Link>
            <Link
              to="/soil-analysis"
              className="text-white hover:text-primary-100 transition-colors font-medium focus:outline-none focus:ring-2 focus:ring-white focus:ring-offset-2 focus:ring-offset-primary-600 rounded px-2 py-1"
            >
              {t('nav.soilAnalysis', 'Soil Analysis')}
            </Link>
            <Link
              to="/quality-grading"
              className="text-white hover:text-primary-100 transition-colors font-medium focus:outline-none focus:ring-2 focus:ring-white focus:ring-offset-2 focus:ring-offset-primary-600 rounded px-2 py-1"
            >
              {t('nav.qualityGrading', 'Quality Grading')}
            </Link>
            <Link
              to="/voice-queries"
              className="text-white hover:text-primary-100 transition-colors font-medium focus:outline-none focus:ring-2 focus:ring-white focus:ring-offset-2 focus:ring-offset-primary-600 rounded px-2 py-1"
            >
              {t('nav.voiceQueries', 'Voice Queries')}
            </Link>
            <Link
              to="/planting-advisory"
              className="text-white hover:text-primary-100 transition-colors font-medium focus:outline-none focus:ring-2 focus:ring-white focus:ring-offset-2 focus:ring-offset-primary-600 rounded px-2 py-1"
            >
              {t('nav.plantingAdvisory', 'Planting Advisory')}
            </Link>
            <Link
              to="/about"
              className="text-white hover:text-primary-100 transition-colors font-medium focus:outline-none focus:ring-2 focus:ring-white focus:ring-offset-2 focus:ring-offset-primary-600 rounded px-2 py-1"
            >
              {t('nav.about', 'About')}
            </Link>
          </nav>

          {/* User Profile and Logout */}
          <div className="flex items-center space-x-4">
            {/* User Menu Dropdown */}
            <div className="relative" ref={userMenuRef}>
              <button
                onClick={() => setIsUserMenuOpen(!isUserMenuOpen)}
                className="hidden sm:flex items-center space-x-2 px-3 py-2 rounded-lg hover:bg-primary-700 dark:hover:bg-primary-600 transition-colors focus:outline-none focus:ring-2 focus:ring-white focus:ring-offset-2 focus:ring-offset-primary-600"
                aria-expanded={isUserMenuOpen}
                aria-haspopup="true"
              >
                <UserCircleIcon className="h-8 w-8 text-primary-100" aria-hidden="true" />
                <div className="text-sm text-left">
                  <p className="font-medium">{user?.name || user?.phoneNumber}</p>
                  <p className="text-xs text-primary-100">{t('header.farmer', 'Farmer')}</p>
                </div>
                <ChevronDownIcon 
                  className={`h-4 w-4 text-primary-100 transition-transform ${isUserMenuOpen ? 'rotate-180' : ''}`} 
                  aria-hidden="true" 
                />
              </button>

              {/* Dropdown Menu */}
              {isUserMenuOpen && (
                <div className="absolute right-0 mt-2 w-48 bg-white dark:bg-gray-800 rounded-lg shadow-lg py-1 z-50 border border-gray-200 dark:border-gray-700">
                  <Link
                    to="/profile"
                    className="block px-4 py-2 text-sm text-gray-700 dark:text-gray-200 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors"
                    onClick={() => setIsUserMenuOpen(false)}
                  >
                    <div className="flex items-center space-x-2">
                      <UserCircleIcon className="h-5 w-5" aria-hidden="true" />
                      <span>{t('nav.profile', 'Profile')}</span>
                    </div>
                  </Link>
                  <hr className="my-1 border-gray-200 dark:border-gray-700" />
                  <button
                    onClick={() => {
                      setIsUserMenuOpen(false);
                      logout();
                    }}
                    className="w-full text-left px-4 py-2 text-sm text-red-600 dark:text-red-400 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors"
                  >
                    {t('auth.logout', 'Logout')}
                  </button>
                </div>
              )}
            </div>

            {/* Mobile: Show only logout button */}
            <button
              onClick={logout}
              className="sm:hidden px-3 py-1.5 bg-white text-primary-600 rounded-lg hover:bg-gray-100 transition-colors font-medium text-sm focus:outline-none focus:ring-2 focus:ring-white focus:ring-offset-2 focus:ring-offset-primary-600"
              aria-label={t('auth.logoutAriaLabel', 'Log out of your account')}
            >
              {t('auth.logout', 'Logout')}
            </button>
          </div>
        </div>
      </div>
    </header>
  );
};
