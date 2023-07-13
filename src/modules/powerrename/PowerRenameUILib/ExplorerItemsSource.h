#pragma once

#include "winrt/Windows.UI.Xaml.h"
#include "winrt/Windows.UI.Xaml.Markup.h"
#include "winrt/Windows.UI.Xaml.Interop.h"
#include "winrt/Windows.UI.Xaml.Controls.Primitives.h"
#include "ExplorerItemsSource.g.h"
#include "ExplorerItemViewModel.h"

#include <PowerRenameInterfaces.h>

using winrt::Microsoft::UI::Xaml::Data::ItemIndexRange;
using winrt::Windows::Foundation::IInspectable;
using namespace winrt::Windows::Foundation::Collections;

extern CComPtr<IPowerRenameManager> g_prManager;

namespace winrt::PowerRenameUI::implementation
{
    struct ExplorerItemProxyIterator
    {
        using iterator_category = std::random_access_iterator_tag;
        using value_type = IInspectable;
        using difference_type = std::ptrdiff_t;
        using pointer = uint32_t;
        using reference = IInspectable;

        const bool* _filtered = nullptr;

        ExplorerItemProxyIterator(pointer ptr, const bool* filtered) :
            _index(ptr), _filtered{ filtered } {}

        reference operator*() const
        {
            const uint32_t realIndex = g_prManager->GetVisibleItemRealIndex(_index);
            if (*_filtered)
            {
                return winrt::make<ExplorerItemViewModel>(realIndex);
            }
            else
                return winrt::make<ExplorerItemViewModel>(_index);
        }

        ExplorerItemProxyIterator& operator++()
        {
            _index++;
            return *this;
        }
        ExplorerItemProxyIterator operator++(int)
        {
            ExplorerItemProxyIterator tmp = *this;
            ++(*this);
            return tmp;
        }
        ExplorerItemProxyIterator& operator--()
        {
            _index--;
            return *this;
        }
        ExplorerItemProxyIterator operator--(int)
        {
            ExplorerItemProxyIterator tmp = *this;
            --(*this);
            return tmp;
        }

        friend bool operator==(const ExplorerItemProxyIterator& a, const ExplorerItemProxyIterator& b) { return a._index == b._index; };
        friend bool operator!=(const ExplorerItemProxyIterator& a, const ExplorerItemProxyIterator& b) { return a._index != b._index; };

        ExplorerItemProxyIterator& operator+=(difference_type n)
        {
            _index += static_cast<pointer>(n);
            return *this;
        }
        ExplorerItemProxyIterator operator+(difference_type n) const { return ExplorerItemProxyIterator{ static_cast<pointer>(_index + n), _filtered }; }
        friend ExplorerItemProxyIterator operator+(difference_type n, const ExplorerItemProxyIterator& iter) { return ExplorerItemProxyIterator(static_cast<pointer>(n + iter._index), iter._filtered); }

        ExplorerItemProxyIterator& operator-=(difference_type n)
        {
            _index -= static_cast<pointer>(n);
            return *this;
        }
        ExplorerItemProxyIterator operator-(difference_type n) const { return ExplorerItemProxyIterator{ static_cast<pointer>(_index - n), _filtered }; }
        difference_type operator-(const ExplorerItemProxyIterator& iter) const { return _index - iter._index; }

        friend bool operator<(const ExplorerItemProxyIterator& a, const ExplorerItemProxyIterator& b) { return a._index < b._index; }
        friend bool operator>(const ExplorerItemProxyIterator& a, const ExplorerItemProxyIterator& b) { return a._index > b._index; }
        friend bool operator<=(const ExplorerItemProxyIterator& a, const ExplorerItemProxyIterator& b) { return a._index <= b._index; }
        friend bool operator>=(const ExplorerItemProxyIterator& a, const ExplorerItemProxyIterator& b) { return a._index >= b._index; }

    private:
        pointer _index;
    };

    using ItemT = IInspectable;
    struct ExplorerItemsSource : ExplorerItemsSourceT<ExplorerItemsSource>, winrt::vector_view_base<ExplorerItemsSource, ItemT>
    {
        bool filtered = false;

        struct Container
        {
            ExplorerItemProxyIterator first;
            ExplorerItemProxyIterator last;

            auto begin() const noexcept
            {
                return first;
            }

            auto end() const noexcept
            {
                return last;
            }
        } container = { { {}, {} }, { {}, {} } };

        void SetIsFiltered(const bool value)
        {
            filtered = value;

            if (!g_prManager)
                return;

            uint32_t item_count = 0;
            if (value)
                winrt::check_hresult(g_prManager->GetVisibleItemCount(&item_count));
            else
                winrt::check_hresult(g_prManager->GetItemCount(&item_count));

            container = Container{ { 0, &filtered }, { item_count, &filtered } };
        }

        auto& get_container() const noexcept
        {
            return container;
        }

        // IObservableVector. We don't need a full implementation, since only the items' properties changes

        Windows::Foundation::Collections::IVectorView<ItemT> GetView() const noexcept
        {
            return static_cast<ExplorerItemsSource const&>(*this);
        }

        winrt::event_token VectorChanged(Windows::Foundation::Collections::VectorChangedEventHandler<ItemT> const& handler)
        {
            return m_changed.add(handler);
        }

        void VectorChanged(event_token const cookie)
        {
            m_changed.remove(cookie);
        }

        winrt::event<Windows::Foundation::Collections::VectorChangedEventHandler<ItemT>> m_changed;

        struct args : implements<args, Windows::Foundation::Collections::IVectorChangedEventArgs>
        {
            args(Windows::Foundation::Collections::CollectionChange const change, uint32_t const index) noexcept :
                m_change(change),
                m_index(index)
            {
            }

            Windows::Foundation::Collections::CollectionChange CollectionChange() const noexcept
            {
                return m_change;
            }

            uint32_t Index() const noexcept
            {
                return m_index;
            }

        private:
            Windows::Foundation::Collections::CollectionChange const m_change;
            uint32_t const m_index;
        };

        void call_changed(Windows::Foundation::Collections::CollectionChange const change, uint32_t const index)
        {
            m_changed(static_cast<ExplorerItemsSource const&>(*this), make<args>(change, index));
        }

        void InvalidateCollection()
        {
            SetIsFiltered(filtered);
            call_changed(Windows::Foundation::Collections::CollectionChange::Reset, 0);
        }

        void InvalidateItemRange(uint32_t const startIdx, uint32_t const count)
        {
            for (uint32_t index = startIdx; index < startIdx + count; ++index)
                call_changed(Windows::Foundation::Collections::CollectionChange::ItemChanged, index);
        }

        void SetAt(uint32_t const index, ItemT const& /*value*/)
        {
            call_changed(Windows::Foundation::Collections::CollectionChange::ItemChanged, index);
        }

        void InsertAt(uint32_t const index, ItemT const& /*value*/)
        {
            call_changed(Windows::Foundation::Collections::CollectionChange::ItemInserted, index);
        }

        void RemoveAt(uint32_t const index)
        {
            call_changed(Windows::Foundation::Collections::CollectionChange::ItemRemoved, index);
        }

        void Append(ItemT const& /*value*/)
        {
            call_changed(Windows::Foundation::Collections::CollectionChange::ItemInserted, this->Size() - 1);
        }

        void RemoveAtEnd()
        {
            call_changed(Windows::Foundation::Collections::CollectionChange::ItemRemoved, this->Size());
        }

        void Clear()
        {
            call_changed(Windows::Foundation::Collections::CollectionChange::Reset, 0);
        }

        void ReplaceAll(array_view<ItemT const> /*value*/)
        {
            call_changed(Windows::Foundation::Collections::CollectionChange::Reset, 0);
        }

        // IClosable
        void Close() noexcept
        {
        }

        // ISelectionInfo
        void SelectRange(const ItemIndexRange& /*itemIndexRange*/) noexcept
        {
            return;
        }

        void DeselectRange(const ItemIndexRange& /*itemIndexRange*/) noexcept
        {
            return;
        }

        bool IsSelected(int32_t /*index*/) noexcept
        {
            return false;
        }

        IVectorView<ItemIndexRange> GetSelectedRanges() noexcept
        {
            return {};
        }

        // IItemsRangeInfo
        void RangesChanged(const ItemIndexRange& /*visibleRange*/, const IVectorView<ItemIndexRange>& /*trackedItems*/)
        {
        }

        //INotifyPropertyChanged
        winrt::event_token PropertyChanged(winrt::Microsoft::UI::Xaml::Data::PropertyChangedEventHandler const& handler)
        {
            return m_propertyChanged.add(handler);
        }

        void PropertyChanged(winrt::event_token const& token) noexcept
        {
            m_propertyChanged.remove(token);
        }

        winrt::event<Microsoft::UI::Xaml::Data::PropertyChangedEventHandler> m_propertyChanged;

        void RaisePropertyChanged(hstring const& propertyName)
        {
            m_propertyChanged(*this, Microsoft::UI::Xaml::Data::PropertyChangedEventArgs(propertyName));
        }
    };
}

namespace winrt::PowerRenameUI::factory_implementation
{
    struct ExplorerItemsSource : ExplorerItemsSourceT<ExplorerItemsSource, implementation::ExplorerItemsSource>
    {
    };
}
