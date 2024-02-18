#include "pch.h"
#include <powertoys/ptarmor/ptarmor.h>
#include <powertoys/ptarmor/ptarmor_activatable_class.h>

namespace powertoys {
    namespace ptarmor {

        // The activation factory for the Echo PowerToy.
        class EchoActivationFactory : public ActivationFactory {
        public:
            // Create an instance of the Echo PowerToy.
            HRESULT CreateInstance(IActivationFactory* factory, REFIID iid, void** out) override {
                *out = new Echo();
                return S_OK;
            }
        };

        // The Echo PowerToy class.
        class Echo : public ActivatableClass {
        public:
            // Initialize the Echo PowerToy.
            HRESULT RuntimeClassInitialize() override {
                return S_OK;
            }

            // Activate the Echo PowerToy.
            HRESULT Activate() override {
                OutputDebugStringA("Echo PowerToy: Hello, World!\n");
                return S_OK;
            }
        };

        // Register the Echo PowerToy.
        const ActivationFactory* RegisterEchoActivationFactory() {
            static ActivationFactoryFactory<EchoActivationFactory> factory;
            return &factory;
        }

    } // namespace ptarmor
} // namespace powertoys
