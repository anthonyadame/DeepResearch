#!/usr/bin/env python
"""
Validation script for Lightning Store implementation
Checks if agentlightning is available and lightning_store can initialize
"""
import sys
import os

def validate_lightning_store():
    """Validate lightning_store setup"""
    print("=" * 60)
    print("🔍 Lightning Store Validation")
    print("=" * 60)
    
    # Check Python version
    print(f"\n📦 Python Version: {sys.version}")
    py_version = float(f"{sys.version_info.major}.{sys.version_info.minor}")
    if py_version < 3.10:
        print(f"   ⚠️  WARNING: Python {py_version} < 3.10 required for agentlightning")
        print(f"      agentlightning requires: Python >=3.10")
    else:
        print(f"   ✅ Python {py_version} meets minimum requirement (>=3.10)")
    
    # Check if agentlightning is installed
    print("\n📦 Checking agentlightning installation...")
    try:
        import agentlightning
        print(f"   ✅ agentlightning is installed")
        print(f"      Version: {agentlightning.__version__ if hasattr(agentlightning, '__version__') else 'unknown'}")
        
        # Try to import required components
        try:
            from agentlightning import InMemoryLightningStore, LightningStoreServer
            print(f"   ✅ InMemoryLightningStore available")
            print(f"   ✅ LightningStoreServer available")
            
            # Try to instantiate
            print("\n🚀 Testing InMemoryLightningStore instantiation...")
            try:
                store = InMemoryLightningStore(thread_safe=True)
                print(f"   ✅ InMemoryLightningStore created successfully")
                print(f"      Type: {type(store)}")
                print(f"      Thread-safe: True")
                
                # Check if store has required methods
                required_methods = [
                    'enqueue_rollout',
                    'query_rollouts',
                    'update_rollout',
                    'get_rollout',
                    'statistics'
                ]
                
                missing_methods = []
                for method in required_methods:
                    if hasattr(store, method):
                        print(f"   ✅ store.{method}() available")
                    else:
                        print(f"   ❌ store.{method}() NOT available")
                        missing_methods.append(method)
                
                if not missing_methods:
                    print("\n" + "=" * 60)
                    print("✅ Lightning Store VALIDATED SUCCESSFULLY")
                    print("=" * 60)
                    print("\nSummary:")
                    print("  • agentlightning is properly installed")
                    print("  • InMemoryLightningStore can be instantiated")
                    print("  • All required methods are available")
                    print("\nYou can start the Lightning Server:")
                    print("  python Docker/lightning-server/server.py")
                    print("=" * 60)
                    return True
                else:
                    print(f"\n❌ Missing methods: {', '.join(missing_methods)}")
                    return False
                    
            except Exception as e:
                print(f"   ❌ Failed to instantiate InMemoryLightningStore: {e}")
                import traceback
                traceback.print_exc()
                return False
                
        except ImportError as e:
            print(f"   ❌ Failed to import required classes: {e}")
            return False
            
    except ImportError as e:
        print(f"   ❌ agentlightning is NOT installed")
        print(f"\nTo install agentlightning:")
        print(f"  1. Upgrade Python to 3.10+:")
        print(f"     https://www.python.org/downloads/")
        print(f"\n  2. Then install agentlightning:")
        if py_version >= 3.10:
            print(f"     pip install git+https://github.com/microsoft/agent-lightning.git")
        else:
            print(f"     pip install git+https://github.com/microsoft/agent-lightning.git")
        print(f"\n  OR use Docker:")
        print(f"     docker-compose up lightning-server")
        return False
    
    except Exception as e:
        print(f"   ❌ Unexpected error: {e}")
        import traceback
        traceback.print_exc()
        return False

if __name__ == "__main__":
    success = validate_lightning_store()
    sys.exit(0 if success else 1)
