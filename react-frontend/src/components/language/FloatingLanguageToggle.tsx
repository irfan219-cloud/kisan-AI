import React, { Fragment, useState } from 'react';
import { Listbox, Transition } from '@headlessui/react';
import { CheckIcon, LanguageIcon } from '@heroicons/react/24/outline';
import { useLanguage } from '@/contexts/LanguageContext';
import { clsx } from 'clsx';

/**
 * FloatingLanguageToggle Component
 * 
 * A fixed-position floating button that allows users to switch languages.
 * Positioned on the right edge of the screen, vertically centered.
 * 
 * Features:
 * - Fixed positioning that remains visible during scrolling
 * - Compact icon-only display
 * - Dropdown menu with language options
 * - Responsive design for mobile devices
 * - Accessible with proper ARIA labels
 */
export const FloatingLanguageToggle: React.FC = () => {
  const { currentLanguage, changeLanguage, supportedLanguages, t } = useLanguage();
  const [isOpen, setIsOpen] = useState(false);

  return (
    <div 
      className="fixed right-4 top-1/2 -translate-y-1/2 z-50"
      role="region"
      aria-label={t('settings.language') || 'Language selector'}
    >
      <Listbox value={currentLanguage.code} onChange={changeLanguage}>
        {({ open }) => (
          <div className="relative">
            <Listbox.Button
              className={clsx(
                'flex items-center justify-center',
                'w-12 h-12 md:w-14 md:h-14',
                'rounded-full shadow-lg',
                'bg-white dark:bg-gray-800',
                'border-2 border-primary-500 dark:border-primary-400',
                'hover:bg-primary-50 dark:hover:bg-gray-700',
                'focus:outline-none focus-visible:ring-2 focus-visible:ring-primary-500 focus-visible:ring-offset-2',
                'transition-all duration-200',
                'cursor-pointer'
              )}
              aria-label={`${t('settings.language') || 'Language'}: ${currentLanguage.nativeName}`}
              onClick={() => setIsOpen(!isOpen)}
            >
              <LanguageIcon 
                className="h-6 w-6 md:h-7 md:w-7 text-primary-600 dark:text-primary-400" 
                aria-hidden="true" 
              />
            </Listbox.Button>

            <Transition
              as={Fragment}
              show={open}
              enter="transition ease-out duration-100"
              enterFrom="transform opacity-0 scale-95"
              enterTo="transform opacity-100 scale-100"
              leave="transition ease-in duration-75"
              leaveFrom="transform opacity-100 scale-100"
              leaveTo="transform opacity-0 scale-95"
            >
              <Listbox.Options 
                className={clsx(
                  'absolute right-0 bottom-full mb-2',
                  'w-48 md:w-56',
                  'origin-bottom-right',
                  'rounded-lg shadow-xl',
                  'bg-white dark:bg-gray-800',
                  'border border-gray-200 dark:border-gray-700',
                  'py-1',
                  'focus:outline-none',
                  'max-h-60 overflow-auto'
                )}
              >
                {supportedLanguages.map((language) => (
                  <Listbox.Option
                    key={language.code}
                    value={language.code}
                    className={({ active }) =>
                      clsx(
                        'relative cursor-pointer select-none py-3 pl-10 pr-4',
                        active 
                          ? 'bg-primary-100 dark:bg-primary-900 text-primary-900 dark:text-primary-100' 
                          : 'text-gray-900 dark:text-gray-100'
                      )
                    }
                  >
                    {({ selected }) => (
                      <>
                        <span
                          className={clsx(
                            'block truncate',
                            selected ? 'font-semibold' : 'font-normal'
                          )}
                        >
                          {language.nativeName}
                        </span>
                        {selected && (
                          <span className="absolute inset-y-0 left-0 flex items-center pl-3 text-primary-600 dark:text-primary-400">
                            <CheckIcon className="h-5 w-5" aria-hidden="true" />
                          </span>
                        )}
                      </>
                    )}
                  </Listbox.Option>
                ))}
              </Listbox.Options>
            </Transition>
          </div>
        )}
      </Listbox>
    </div>
  );
};
