// Q14. REACT PERFORMANCE — display 1 million products efficiently

import React, {
  useState, useEffect, useCallback, useRef, useMemo
} from "react";
import { FixedSizeList as VirtualList } from "react-window"; // virtualization library


// 1. DEBOUNCE HOOK
// "Wait until user stops typing for 400ms, then run"
// Without this: API call fires on every single keystroke
function useDebounce(value, delay = 400) {
  const [debounced, setDebounced] = useState(value);

  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delay); // set timer
    return () => clearTimeout(timer); // cancel timer if value changes before delay
  }, [value, delay]);

  return debounced;
}


// 2. SINGLE PRODUCT ROW (memoized)
// React.memo = only re-renders if props actually changed
// Without this: ALL rows re-render when parent state changes

const ProductRow = React.memo(({ index, style, data }) => {
  const product = data[index];

  if (!product) {
    // Show placeholder while loading next batch
    return <div style={style} className="row loading">Loading...</div>;
  }

  return (
    // "style" from react-window positions the row absolutely — DO NOT remove it
    <div style={style} className="row">
      <span>{product.name}</span>
      <span>${product.price}</span>
      <span>{product.brand}</span>
    </div>
  );
});


// 3. MAIN COMPONENT
const PAGE_SIZE    = 50;   // load 50 products per API call
const ROW_HEIGHT   = 50;   // each row is 50px tall (required by react-window)
const LIST_HEIGHT  = 600;  // visible list window height

export default function ProductList() {
  const [search,   setSearch]   = useState("");
  const [products, setProducts] = useState([]);
  const [page,     setPage]     = useState(1);
  const [hasMore,  setHasMore]  = useState(true);
  const [loading,  setLoading]  = useState(false);

  // Debounced search — only triggers fetch after user stops typing
  const debouncedSearch = useDebounce(search, 400);

  // Ref to hold the AbortController — lets us cancel in-flight API requests
  const abortRef = useRef(null);

  //FETCH FUNCTION
  // useCallback = stable function reference, won't re-create on every render
  const fetchProducts = useCallback(async (searchTerm, pageNum, reset = false) => {
    // Cancel any previous in-flight request
    // e.g. user types fast — cancel old search, run new one
    if (abortRef.current) abortRef.current.abort();
    abortRef.current = new AbortController();

    setLoading(true);
    try {
      const res = await fetch(
        `/api/products?keyword=${searchTerm}&page=${pageNum}&pageSize=${PAGE_SIZE}`,
        { signal: abortRef.current.signal } // attach cancel signal
      );
      const data = await res.json();

      setProducts(prev =>
        reset
          ? data.items                  // new search - replace list
          : [...prev, ...data.items]    // scroll - append to existing list
      );

      // If API returned fewer items than page size, no more pages left
      setHasMore(data.items.length === PAGE_SIZE);
    } catch (err) {
      if (err.name === "AbortError") return; // request was cancelled — ignore
      console.error("Fetch failed:", err);
    } finally {
      setLoading(false);
    }
  }, []);

  //RESET on new search 
  useEffect(() => {
    setProducts([]);   // clear old results
    setPage(1);        // reset to page 1
    fetchProducts(debouncedSearch, 1, true); // reset=true - replace list
  }, [debouncedSearch, fetchProducts]);

  //INFINITE SCROLL — load next page 
  // Called by react-window when user scrolls near the bottom
  const onScroll = useCallback(({ scrollOffset }) => {
    const totalHeight   = products.length * ROW_HEIGHT;
    const scrollBottom  = scrollOffset + LIST_HEIGHT;
    const nearBottom    = scrollBottom >= totalHeight - 200; // 200px before end

    if (nearBottom && hasMore && !loading) {
      const nextPage = page + 1;
      setPage(nextPage);
      fetchProducts(debouncedSearch, nextPage); // load next batch
    }
  }, [products.length, hasMore, loading, page, debouncedSearch, fetchProducts]);

  //MEMOIZED item count
  // useMemo = recalculate only when products/hasMore changes
  // Adds 1 extra "Loading..." slot at the end while more pages exist
  const itemCount = useMemo(
    () => hasMore ? products.length + 1 : products.length,
    [products.length, hasMore]
  );

  return (
    <div>
      {/* Search box */}
      <input
        placeholder="Search products..."
        value={search}
        onChange={e => setSearch(e.target.value)} // debounce handles the delay
      />
      {loading && <span>Loading...</span>}

      {/*
        VIRTUALIZATION via react-window FixedSizeList:
        Only renders the ~12 rows visible on screen at any time.
        Without this: rendering 1M <div> nodes = browser crashes.
        With this: always ~12 DOM nodes regardless of list size.
      */}
      <VirtualList
        height={LIST_HEIGHT}   // visible area height
        itemCount={itemCount}  // total item slots
        itemSize={ROW_HEIGHT}  // each row height in px
        itemData={products}    // passed as "data" prop to ProductRow
        onScroll={onScroll}    // fires on scroll — triggers infinite load
        width="100%"
      >
        {ProductRow}
      </VirtualList>
    </div>
  );
}


// HOW EACH FEATURE WORKS — SUMMARY

// Virtualization        - react-window only renders visible rows (~12 at a time)
// Infinite Scrolling    - onScroll detects near-bottom - fetch next page - append
// Debounced Search      - wait 400ms after typing stops - 1 API call, not 100
// API Request Cancel    - AbortController cancels stale requests on new search
// Memoization           - React.memo on rows, useMemo for itemCount, useCallback for functions